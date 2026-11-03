using CB2DbQueryGovernance;
using CB2DbQueryGovernance;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static System.Runtime.InteropServices.JavaScript.JSType;



namespace CB2DbQueryGovernance
{
    internal class DecisionPredicateValidator
    {

        private string _modelName { get; }
        private string _userrequest { get; }
        private string _instructions { get; }
        private Dictionary<string, string> _criteria { get; }


        public DecisionPredicateValidator(string modelName, string instructions, Dictionary<string, string> criteria)
        {
            _modelName = modelName;
            _instructions = instructions;
            _criteria = criteria;

        }

        public async Task<DecissionResponse> ValidatePredicate(string FoundryApiKey, string FoundryUrl, string userrequest, double threshold)
        {
            HttpClient _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", FoundryApiKey);


            var request = new DecisionRequest
            {
                Model = _modelName,  //deployment
                State = userrequest, // The user's incoming request
                Questions = new Dictionary<string, DecisionQuestion>
                {
                    ["decision"] = new DecisionQuestion
                    {
                        Type = "choice",
                        Instructions = _instructions,
                        Criteria = _criteria
                    }
                }
            };

            try
            {

                // Serialize the payload before sending it.
                string json = JsonSerializer.Serialize(request);
                byte[] body = Encoding.UTF8.GetBytes(json);

                using var content = new ByteArrayContent(body);
                content.Headers.ContentType =
                    new MediaTypeHeaderValue("application/json");
                content.Headers.ContentLength = body.Length;

                using var response = await _httpClient.PostAsync(FoundryUrl, content);




                if (response.IsSuccessStatusCode)
                {

                    string responseJson = await response.Content.ReadAsStringAsync();
                    using JsonDocument document = JsonDocument.Parse(responseJson);
                    JsonElement root = document.RootElement;

                    Console.WriteLine("\n--- Microsoft-Decision-1 Results ---");

                    if (!root.TryGetProperty("answers", out JsonElement answers) ||
                        !answers.TryGetProperty("decision", out JsonElement answer) ||
                        !answer.TryGetProperty("probabilities", out JsonElement probabilities))
                    {
                        DecissionResponse decisionResponse1 = new DecissionResponse
                        {
                            Value = "UNKNOWN",
                            Probability = "NA"
                        };

                        return decisionResponse1;
                    }

                    var bestChoice = probabilities.EnumerateObject()
                        .OrderByDescending(p => p.Value.GetDouble())
                        .FirstOrDefault();


                    if (bestChoice.Name is null ||bestChoice.Value.GetDouble() <= (bestChoice.Name == "ESCALATE" ? 0.6 : threshold))
                    {
                        Console.WriteLine("Selected decision: UNKNOWN");

                        DecissionResponse decisionResponse1 = new DecissionResponse
                        {
                            Value = "UNKNOWN",
                            Probability = "NA"
                        };

                        return decisionResponse1;
                    }

                    string selectedChoice = bestChoice.Name;
                    double selectedProbability = bestChoice.Value.GetDouble();

                    Console.WriteLine(
                        $"Selected decision: {selectedChoice} " +
                        $"(Probability: {selectedProbability:P2})");

                    DecissionResponse decisionResponse = new DecissionResponse
                    {
                        Value = selectedChoice,
                        Probability = selectedProbability.ToString("P2")
                    };

                    return decisionResponse;

                }
                else
                {
                    DecissionResponse decisionResponse = new DecissionResponse
                    {
                        Value = "UNKNOWN",
                        Probability = "NA"
                    };

                    return decisionResponse;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred: {ex.Message}");
                DecissionResponse decisionResponse = new DecissionResponse
                {
                    Value = "ERROR",
                    Probability = "NA"
                };

                return decisionResponse;


            }


        }
    }

    public class DecissionResponse
    {
        public string Value { get; set; }
        public string Probability { get; set; }
    }
}




