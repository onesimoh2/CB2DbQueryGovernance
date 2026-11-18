using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace CB2DbQueryGovernance
{



    public class SqlSafetyChecker
    {
        // A visitor that tracks any statement that IS NOT a SelectStatement
        private class SelectOnlyVisitor : TSqlFragmentVisitor
        {
            public bool HasNonSelectClauses { get; private set; } = false;
            public List<string> Violations { get; } = new List<string>();

            // We override the base TSqlStatement visitor method.
            // This catches ALL high-level statements (Insert, Update, Delete, Drop, Alter, etc.)
            public override void Visit(TSqlStatement node)
            {
                // If the statement is structurally a SelectStatement, it's safe.
                if (node is SelectStatement)
                {
                    base.Visit(node);
                    return;
                }

                // If it's anything else, log it as a violation
                HasNonSelectClauses = true;
                Violations.Add(node.GetType().Name);

                base.Visit(node);
            }
        }

        public  (bool IsSafe, List<string> Violations) CheckIfSelectOnly(string sqlQuery)
        {
            // 1. Initialize the correct parser version (e.g., TSql160 targets SQL Server 2022)
            var parser = new TSql160Parser(true);

            using (var reader = new StringReader(sqlQuery))
            {
                // 2. Parse the string into an AST fragment tree
                TSqlFragment fragment = parser.Parse(reader, out IList<ParseError> errors);

                // If there are syntax errors, it's unsafe or malformed
                if (errors.Count > 0)
                {
                    var syntaxErrors = new List<string>();
                    foreach (var err in errors)
                    {
                        syntaxErrors.Add($"Syntax Error line {err.Line}: {err.Message}");
                    }
                    return (false, syntaxErrors);
                }

                // 3. Fire up the visitor to inspect the structural nodes
                var visitor = new SelectOnlyVisitor();
                fragment.Accept(visitor);

                return (!visitor.HasNonSelectClauses, visitor.Violations);
            }
        }

    }



    public class SqlTableGatekeeper
    {
        // 1. The visitor now accepts an allowed collection via its constructor
        private class StrictTableVisitor : TSqlFragmentVisitor
        {
            private readonly HashSet<string> _allowedTables;

            public bool HasViolations { get; private set; } = false;
            public List<string> Violations { get; } = new List<string>();

            public StrictTableVisitor(IEnumerable<string> allowedTables)
            {
                // Use StringComparer.OrdinalIgnoreCase to ensure case-insensitivity
                _allowedTables = new HashSet<string>(allowedTables, StringComparer.OrdinalIgnoreCase);
            }

            //public override void Visit(NamedTableReference node)
            //{
            //    string tableName = node.SchemaObject.BaseIdentifier.Value;

            //    // Check if the extracted table name exists in the allowed set
            //    if (!_allowedTables.Contains(tableName))
            //    {
            //        HasViolations = true;
            //        Violations.Add($"Unauthorized table access attempted: '{tableName}'");
            //    }

            //    base.Visit(node);
            //}

            public override void Visit(NamedTableReference node)
            {
                string tableName = string.Join(".",
                    node.SchemaObject.Identifiers.Select(i => i.Value));

                if (!_allowedTables.Contains(tableName))
                {
                    HasViolations = true;
                    Violations.Add($"Unauthorized table access attempted: '{tableName}'");
                }

                base.Visit(node);
            }
        }

        // 2. Expose the collection signature on your public validation method
        public  (bool IsSafe, List<string> Violations) ValidateQuery(string sqlQuery, IEnumerable<string> allowedTables)
        {
            var parser = new TSql160Parser(true);

            using (var reader = new StringReader(sqlQuery))
            {
                TSqlFragment fragment = parser.Parse(reader, out IList<ParseError> errors);

                if (errors.Count > 0)
                {
                    return (false, new List<string> { "Malformed SQL Syntax." });
                }

                //if (fragment is TSqlScript script &&
                //   (script.Batches.Count > 1 || script.Batches.Statements.Count > 1))
                //{
                //    return (false, new List<string> { "Multi-statement batching detected. Only one query allowed." });
                //}

                // Pass the custom collection down to the AST visitor instance
                var visitor = new StrictTableVisitor(allowedTables);
                fragment.Accept(visitor);

                return (!visitor.HasViolations, visitor.Violations);
            }
        }
    }

    internal class ValidateGeneratedSQL
    {
        public async Task<ValidationReturn>  CheckSqlSintax(string sqlStr, string sqlSchema, string sessionId, string userRequest) 
        {
            GovernanceRecord govRec = new();
            ValidationReturn req = new();


            string connectionString = Environment.GetEnvironmentVariable("SQL_CONN")!;

            SqlSafetyChecker sqlSafetyChecker = new();
            var safeResult = sqlSafetyChecker.CheckIfSelectOnly(sqlStr);
            LogGovernanceRecords logGovRec = new LogGovernanceRecords();

            if (!safeResult.IsSafe)
            {
                govRec.SessionId = sessionId;
                govRec.Category = "SQL_VALIDATION_ERROR";
                govRec.UserRequest = "Original request: " + userRequest;
                govRec.ProposedAction = "Block execution due to error in the generated SQL that might be result of a security violation";
                govRec.Decision = "ERROR";
                govRec.DecisionSource = "SQL_VALIDATION";
                govRec.Threshold = Convert.ToDecimal("1.0");
                govRec.Reason = "The governance evaluation failed with following message: " + string.Join(", ", safeResult.Violations);
                govRec.Executed = false;
                govRec.Outcome = "Blocked pending recovery and successful governance evaluation.";

                string logResult = await logGovRec.CreateRecord(connectionString, govRec);
                req.code = "ERROR";
                req.message = "An error occurred during SQL validation.";
                return req;
            }

            // Define your approved runtime rules dynamically
            using System.Text.Json.JsonDocument doc = JsonDocument.Parse(sqlSchema);
            string dbTable = doc.RootElement.GetProperty("table").GetString() ?? string.Empty;

            var dynamicAllowList = new List<string> { dbTable };

            SqlTableGatekeeper sqlTableGatekeeper = new();

            // Pass the query along with your context rules
            var result = sqlTableGatekeeper.ValidateQuery(sqlStr, dynamicAllowList);
            if (!result.IsSafe)
            {
                govRec.SessionId = sessionId;
                govRec.Category = "SQL_VALIDATION_ERROR";
                govRec.UserRequest = "Original request: " + userRequest;
                govRec.ProposedAction = "Block execution due to error in the generated SQL that might be result of a security violation";
                govRec.Decision = "ERROR";
                govRec.DecisionSource = "SQL_VALIDATION";
                govRec.Threshold = Convert.ToDecimal("1.0");
                govRec.Reason = "The governance evaluation failed with following message: " + string.Join(", ", safeResult.Violations);
                govRec.Executed = false;
                govRec.Outcome = "Blocked pending recovery and successful governance evaluation.";

                string logResult = await logGovRec.CreateRecord(connectionString, govRec);
                req.code = "ERROR";
                req.message = "An error occurred during SQL validation.";
                return req;
            }
            req.code = "OK";
            req.message = "SQL validation passed.";

            return req;
        }

    }

}
