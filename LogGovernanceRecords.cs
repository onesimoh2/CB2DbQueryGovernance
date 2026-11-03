using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Text;

namespace CB2DbQueryGovernance
{
    internal class LogGovernanceRecords
    {

        public async Task<string> CreateRecord(string connectionString, GovernanceRecord govRec)
        {

            const string sql = """
            INSERT INTO [AmlBeyond].[amlb].AIGovernanceLog
            (
                SessionId,
                Category,
                UserRequest,
                ProposedAction,
                Decision,
                DecisionSource,
                Confidence,
                Threshold,
                Reason,
                Executed,
                Outcome
            )
            VALUES
            (
                @SessionId,
                @Category,
                @UserRequest,
                @ProposedAction,
                @Decision,
                @DecisionSource,
                @Confidence,
                @Threshold,
                @Reason,
                @Executed,
                @Outcome
            );
            """;
            try
            {

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@SessionId", govRec.SessionId);
            command.Parameters.AddWithValue("@Category", govRec.Category);
            command.Parameters.AddWithValue("@UserRequest", govRec.UserRequest);
            command.Parameters.AddWithValue("@ProposedAction", (object?)govRec.ProposedAction ?? DBNull.Value);
            command.Parameters.AddWithValue("@Decision", govRec.Decision);
            command.Parameters.AddWithValue("@DecisionSource", govRec.DecisionSource);
            command.Parameters.AddWithValue("@Confidence", govRec.Confidence);
            command.Parameters.AddWithValue("@Threshold", govRec.Threshold);
            command.Parameters.AddWithValue("@Reason", govRec.Reason);
            command.Parameters.AddWithValue("@Executed", govRec.Executed);
            command.Parameters.AddWithValue("@Outcome",
                (object?)govRec.Outcome ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();
            return "OK";
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error occurred while logging governance record: {ex.Message}");
                return "ERROR";

            }

        }
    }

    public class GovernanceRecord
    {
        public long GovernanceId { get; set; }

        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string Category { get; set; }

        public string? SessionId { get; set; }

        public string UserRequest { get; set; }

        public string? ProposedAction { get; set; }

        public string Decision { get; set; }
        public string DecisionSource { get; set; }

        public decimal? Confidence { get; set; }

        public decimal? Threshold { get; set; }

        public string? Reason { get; set; }

        public bool Executed { get; set; }

        public string? Outcome { get; set; }
    }
}
