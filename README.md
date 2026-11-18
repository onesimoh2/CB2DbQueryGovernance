# SQL Agent Governance Engineering Prototype

## Overview

This project is an initial validation of an AI-assisted SQL workflow 
designed to query databases through a process governed across the request, 
-generation, validation, and execution stages. The current implementation
has been tested against a single, predefined database and table. 
However, the approach is intended to be extensible, allowing support 
for additional databases and data sources in future versions.

A user submits a natural-language request. The application first
evaluates whether the request is permitted to proceed. If it is, an AI
agent generates a SQL Server `SELECT` statement using a supplied
database schema. The generated SQL is then checked structurally and
semantically before a separate execution agent invokes the SQL execution
tool.

The central design idea is that governance is applied throughout the
workflow rather than relying only on the initial request check or the
final answer. Deterministic controls and semantic evaluation have
different responsibilities and are used together.

This is a proof of concept / initial validation, not a complete
production-ready governance platform.

## Current scope

The supplied schema describes the following SQL Server object:

`AmlBeyond.amlb.d_accounts`

The schema includes descriptions, data types, lengths, related columns,
and sample values. The fields represented in the current schema include:

-   `account`
-   `appl_code`
-   `appl_type`
-   `product_group`
-   `product_type`
-   `product_description`
-   `acct_rec_status`
-   `acct_date_opened`
-   `acct_dormant_date`
-   `acct_cur_bal`
-   `acct_ownership_desc`

The schema is supplied to the SQL-generation and intent-validation
stages so that they can reason about the meaning and permitted structure
of the available data.

The current prototype is focused on read-only SQL Server queries. The
SQL-generation agent is instructed to generate `SELECT` statements only.
The application is responsible for independently checking the generated
statement before it can be executed.

## Workflow

The main orchestration flow is:

1.  Receive the user's natural-language request.
2.  Run initial request governance.
3.  Handle the governance decision.
4.  Generate a SQL statement.
5.  Validate SQL syntax and structural safety.
6.  Validate whether the SQL reflects the original user request.
7.  Record governance decisions.
8.  Execute the approved SQL through a separate execution agent and its
    `sqlFunction` tool.
9.  Return the response to the user.

### 1. Initial request governance --- `InitialValidation`

`InitialValidation.ValidateInitialRequest(...)` evaluates the incoming
request before SQL generation.

The orchestration handles these outcomes:

  -----------------------------------------------------------------------
  Decision code                       Application behavior
  ----------------------------------- -----------------------------------
  `OK_TO_PROCEED`                     Continue to SQL generation.

  `BACK_TO_USER`                      Display the clarification message
                                      and obtain a revised request.

  `ESCALATE`                          Stop the workflow and require a
                                      human escalation path. Not implemented in the current prototype.

  `UNKNOWN`                           Retry the governance evaluation,
                                      with a limited retry count.

  `ERROR`                             Stop processing because of a
                                      technical failure.
  -----------------------------------------------------------------------

The code includes placeholder comments for technical-error recovery and
human escalation. Those comments describe possible future
implementations; they are not themselves implemented escalation or
recovery services.

### 2. SQL generation --- `GenerateSqlScript`

The SQL-generation agent receives the user's request and the database
schema.

Its instructions require it to:

-   Generate SQL Server `SELECT` statements.
-   Use only tables and columns defined in the supplied schema.
-   Avoid inventing database objects or column names.
-   Return the SQL statement on a single line.
-   Never execute SQL.
-   Return a structured `SqlGenerationResult`.

The generation result uses these statuses:

  -----------------------------------------------------------------------
  Status                              Meaning
  ----------------------------------- -----------------------------------
  `SUCCESS`                           The request is clear, within scope,
                                      and can be translated into a SQL
                                      statement.

  `NEEDS_CLARIFICATION`               The request is permitted but
                                      ambiguous or incomplete.

  `DISCARDED`                         The request is outside the
                                      permitted scope or asks for an
                                      operation that is not allowed.
  -----------------------------------------------------------------------



The application should check the generation status before proceeding to
SQL validation. A generated statement should not be treated as
executable merely because the model returned one.

### 3. SQL syntax and structural validation --- `ValidateGeneratedSQL`

The generated SQL is passed to
`ValidateGeneratedSQL.CheckSqlSintax(...)` before semantic intent
validation or execution.

The structural validation stage is intended to enforce SQL-specific
constraints independently of the language model. The project has also
developed a SQL Server ScriptDom-based approach for parsing the
statement into an abstract syntax tree (AST).

The related validation responsibilities include:

-   Confirm that the SQL parses as valid T-SQL.
-   Require a single permitted SQL statement.
-   Permit only the intended read-only query structure.
-   Reject additional statements or prohibited commands.
-   Inspect referenced tables against an allowlist derived from the
    supplied schema.
-   Prevent access to tables outside the permitted scope.

For table comparison, ScriptDom identifier values do not retain the
square-bracket delimiters from SQL text. For example:

-   SQL text: `[AmlBeyond].[amlb].[d_accounts]`
-   Normalized identifier: `AmlBeyond.amlb.d_accounts`

The allowlist should use the same canonical representation, with
case-insensitive comparison where appropriate.

A `SELECT` statement check alone is not a complete read-only policy.
Depending on the application's requirements, the AST policy should also
inspect constructs such as `SELECT INTO`, external data access,
executable functions, and query hints. The precise allowlist should be
defined by the application rather than inferred from a list of keywords.

### 4. SQL intent validation --- `SqlIntentValidator`

Passing syntax and structural checks does not prove that a query answers
the user's question. A syntactically valid, permitted query can still
misinterpret the request.

The `SqlIntentValidator` agent receives:

-   The original user request.
-   The generated SQL statement.
-   The database schema.

It evaluates whether the SQL faithfully represents the user's intent,
including:

-   Selected tables and columns.
-   Filters and comparison values.
-   Logical conditions such as `AND` and `OR`.
-   Joins and relationships.
-   Grouping and aggregation.
-   Sorting and result limits.
-   The expected meaning of the returned result.

It does not generate or rewrite SQL. Its purpose is to evaluate semantic
correspondence between the request and the generated statement.

The structured result uses:

  -----------------------------------------------------------------------
  Status                              Meaning
  ----------------------------------- -----------------------------------
  `VALID`                             The SQL faithfully represents the
                                      request.

  `INVALID`                           The SQL materially misrepresents or
                                      omits part of the request.

  `NEEDS_REVIEW`                      The validator cannot confidently
                                      determine correctness and further
                                      review is required.
  -----------------------------------------------------------------------

The orchestration records the validation outcome. An `INVALID` result
blocks execution. A `NEEDS_REVIEW` result is retried within the
configured limit; if it remains unresolved, execution stops. Only
`VALID` proceeds to execution.

### 5. SQL execution --- `ExecuteSQLAgent`

SQL execution is separated from SQL generation and intent validation.

The execution agent is instructed to use the `sqlFunction` tool to
execute the supplied SQL statement. Its responsibilities are to:

-   Execute only the statement supplied to it.
-   Avoid generating, modifying, or extending the SQL.
-   Avoid executing commands other than the permitted query.
-   Report the actual outcome returned by the tool.
-   Never claim success unless the tool confirms successful execution.

The execution result is intended to be structured and to distinguish
successful execution, known execution failure, and an uncertain outcome
requiring review.

The application then presents the response to the user.

**Important:** The execution tool and the database account behind it
must enforce the read-only boundary independently. Agent instructions
are not a substitute for database permissions or application-side
validation.

## Governance records

The orchestration writes governance records through
`LogGovernanceRecords.CreateRecord(...)`.

The current code records information such as:

-   Session ID.
-   Governance category.
-   User request.
-   Proposed action.
-   Decision.
-   Decision source.
-   Confidence and threshold.
-   Reason.
-   Whether execution occurred.
-   Outcome.

The intent-validation stage records `VALID`, `INVALID`, and
`NEEDS_REVIEW` outcomes, including cases where review attempts are
exhausted.

The current record structure is a prototype-level audit record. A
production implementation may need additional traceability, such as the
generated SQL itself, model/deployment identifiers, prompt or
instruction versions, tool-call details, timestamps, validation results,
and the final execution outcome. Sensitive information should not be
written to logs unnecessarily.

## Configuration

The code reads the following settings from environment variables:

  -----------------------------------------------------------------------
  Environment variable                Purpose
  ----------------------------------- -----------------------------------
  `SQL_CONN`                          SQL Server connection string used
                                      by the application and its
                                      database-related components.

  `AZURE_OPENAI_ENDPOINT`             Endpoint used to create the
                                      OpenAI-compatible client.

  `AZURE_OPENAI_DEPLOYMENT_NAME`      Model deployment name passed to the
                                      agent configuration.

  `AZURE_OPENAI_API_KEY`              API key used to authenticate the
                                      client.
  -----------------------------------------------------------------------

Set these values in the local development environment or a secure
configuration provider. Do not commit API keys or connection strings to
source control.



The endpoint must correspond to the inference API supported by the
deployed model. The endpoint and deployment configuration should be
confirmed in the model deployment's connection or consumption details.

## Main components

The orchestration shown in the current prototype uses or refers to these
components:

  -----------------------------------------------------------------------
  Component                           Responsibility
  ----------------------------------- -----------------------------------
  `InitialValidation`                 Evaluates the original user request
                                      before SQL generation.             -----------------

  `GenerateSqlScript`                 Creates the SQL-generation agent. -----------------

                                     

  `SqlIntentValidator`                Creates the semantic SQL-intent
                                      validation agent.                ------------


  `ExecuteSQLAgent`                   Creates the SQL execution agent. -----------------

  `SQLFunction`                       Tool used by the execution agent to --------------
                                      execute the supplied SQL.

   'SqlIntentValidator'               Evaluates whether the generated SQL
                                      faithfully represents the user's
                                      request.                          -----------------


  `LogGovernanceRecords`              Persists governance records. -----------------
  -----------------------------------------------------------------------

Some of these components are defined outside the supplied orchestration
excerpt. Their exact implementations and signatures should be reviewed
in their respective source files.

## Build and run

The supplied source excerpt does not include the complete project file,
all component implementations, or exact NuGet package versions.
Therefore, the precise package list and build commands must be confirmed
against the solution's `.csproj` files.

The implementation uses C#/.NET, the Microsoft Agent Framework agent
abstractions, an OpenAI-compatible client, SQL Server connectivity, and
SQL Server ScriptDom for AST-based SQL inspection.

At a minimum, confirm that the project references the packages required
by the actual implementations of:

-   Microsoft Agent Framework and its OpenAI integration.
-   The OpenAI .NET SDK and its credential types.
-   SQL Server connectivity (for example, `Microsoft.Data.SqlClient`, if
    used by the database classes).
-   `Microsoft.SqlServer.TransactSql.ScriptDom`, if using the ScriptDom
    validators.

Use the package versions selected by the solution rather than assuming
that all Microsoft Agent Framework packages share the same version
number.

## Current implementation notes and items to verify

The supplied code is an evolving prototype. Before treating it as an
operational application, review the following points:

1.  **Check generation status before parsing or validating SQL.** The
    orchestration excerpt deserializes `SqlGenerationResult` and
    proceeds to syntax validation without showing a branch for
    `NEEDS_CLARIFICATION` or `DISCARDED`.
2.  **Pass useful feedback into SQL regeneration.** The current
    `additional_indication` variable is empty. The intent-validation
    retry loop uses `continue`, but the excerpt does not update the next
    generation prompt with the validator's issues. Without new feedback
    or another intervention, the same SQL may be generated and evaluated
    repeatedly.
3.  **Handle null and malformed agent responses.** Deserialization can
    fail or return `null`; check the result before accessing its
    properties. Also handle unexpected structured output.
4.  **Handle execution status and response properties consistently.**
    The excerpt reads `executeResult.UserResponse`, while the earlier
    `SqlExecutionResult` shape described for this workflow contains
    `Status`, `Explanation`, and `Issues`. Align the DTO, JSON schema,
    and consuming code. If the execution agent must return a user-facing
    answer, add a corresponding property to the DTO.
5.  **Check execution outcome before reporting success.** The
    application should branch on the execution status and should not
    print a success-style answer for `INVALID` or `NEEDS_REVIEW`.
6.  **Preserve the exact SQL that passed validation.** The SQL passed to
    the execution tool should be the same statement that passed
    structural and intent validation. If it changes, validation must be
    repeated.
7.  **Enforce database-level read-only permissions.** Use a database
    identity with only the permissions required for the intended
    queries. Do not rely solely on the agent prompt or AST inspection.
8.  **Complete the escalation and technical-error paths.** The current
    code contains design notes for these paths, but their workflow
    services are placeholders.
9.  **Review retry behavior.** Define what new evidence or changed input
    justifies each retry, set explicit limits, and fail closed when
    validation remains inconclusive.
10. **Improve audit correlation.** Store the relevant SQL and outcome in
    the governance record or in a linked execution record, subject to
    data-retention and privacy requirements.

These are implementation review points, not claims that the
corresponding functionality is already complete.

## Design principle

The prototype separates different governance questions instead of asking
one agent to decide everything:

-   **Request governance:** Is the user's request permitted to proceed?
-   **SQL generation:** Can the request be translated into a query using
    the supplied schema?
-   **Structural governance:** Is the generated SQL syntactically valid
    and within the permitted SQL and table scope?
-   **Semantic governance:** Does the query actually express what the
    user requested?
-   **Execution governance:** Can the approved statement be executed
    under the application's database permissions, and what was the
    actual outcome?

Deterministic controls are used where rules and boundaries can be stated
precisely. Semantic evaluation is used where the application must assess
intent and meaning. The controls operate at different points in the
workflow and should remain independently enforceable.

## Status

This repository should be presented as an **initial validation / proof
of concept for AI Governance Engineering**. It demonstrates a
multi-stage approach to governing an AI-assisted SQL workflow. It is a
starting point for a larger system in which agents may generate SQL,
execute it, and answer users while governance controls, monitoring, and
auditability are applied throughout the process.
