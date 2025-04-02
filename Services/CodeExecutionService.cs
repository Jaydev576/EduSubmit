using System.Collections;
using System.Net.Http.Headers;
using System.Text;
using EduSubmit.Models;
using Newtonsoft.Json;

namespace EduSubmit.Services
{
    public class CodeExecutionService
    {
        private readonly HttpClient _httpClient;
        private const string Judge0ApiUrl = "https://judge0-ce.p.rapidapi.com/submissions";

        public CodeExecutionService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.DefaultRequestHeaders.Add("x-rapidapi-key", "9786e0be57mshba7ae8b53a816f4p1240b3jsn06656a784938");
            _httpClient.DefaultRequestHeaders.Add("x-rapidapi-host", "judge0-ce.p.rapidapi.com");
        }

        public async Task<Dictionary<string, string>> ExecuteCodeAndFetchResults(string programmingLanguage, string code, List<TestCase> testCases)
        {
            int languageId = GetJudge0LanguageId(programmingLanguage);
            if (languageId == -1)
                throw new NotSupportedException($"Language {programmingLanguage} is not supported.");

            // 🔹 Step 1: Submit code for execution
            var batchRequest = new
            {
                submissions = testCases.Select(tc => new
                {
                    source_code = code,
                    language_id = languageId,
                    stdin = tc.Input
                }).ToList() // Ensure it's a list for later use
            };

            var jsonPayload = JsonConvert.SerializeObject(batchRequest);
            Console.WriteLine($"[DEBUG] JSON Payload: {jsonPayload}"); // ✅ Log actual JSON data

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{Judge0ApiUrl}/batch?base64_encoded=false&wait=false", content);
            string responseString = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[DEBUG] API Response: {responseString}");
            response.EnsureSuccessStatusCode();

            // Deserialize the JSON response
            var submissionsList = JsonConvert.DeserializeObject<List<dynamic>>(responseString);

            // Extract the tokens from the submissions
            var tokens = submissionsList.Select(s => (string)s.token).ToList();

            // 🔹 Step 2: Fetch execution results with input as key
            return await FetchExecutionResults(tokens, batchRequest);
        }

        private async Task<Dictionary<string, string>> FetchExecutionResults(List<string> tokens, object batchRequest)
        {
            // Create a mapping between tokens and inputs
            var tokenToInputMap = new Dictionary<string, string>();
            if (batchRequest is { } br && br.GetType().GetProperty("submissions")?.GetValue(br) is IList submissions)
            {
                for (int i = 0; i < tokens.Count && i < submissions.Count; i++)
                {
                    var submission = submissions[i];
                    string input = submission?.GetType().GetProperty("stdin")?.GetValue(submission)?.ToString() ?? "";
                    tokenToInputMap[tokens[i]] = input; // Map token to its input
                }
            }
            else
            {
                throw new ArgumentException("batchRequest must contain a valid submissions list.");
            }

            Dictionary<string, string> executionResults = new(); // Key will be input, not token
            int retryCount = 0;
            const int maxRetries = 10;

            while (retryCount < maxRetries)
            {
                await Task.Delay(1000); // Wait 1 second before polling

                try
                {
                    var tokenQuery = string.Join(",", tokens);
                    string responseQuery = $"{Judge0ApiUrl}/batch?tokens={tokenQuery}&base64_encoded=false&fields=token,stdout,stderr,status_id,language_id";
                    Console.WriteLine($"[DEBUG] API Query: {responseQuery}");

                    var response = await _httpClient.GetAsync(responseQuery);
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[ERROR] API Request Failed: {response.StatusCode}");
                        return executionResults; // Return partial results
                    }

                    string responseString = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[DEBUG] API Response: {responseString}");

                    var results = JsonConvert.DeserializeObject<dynamic>(responseString);
                    if (results == null || results.submissions == null)
                    {
                        Console.WriteLine("[ERROR] No submissions found in response!");
                        throw new Exception("Invalid response from Judge0 API");
                    }

                    var submissionsResponse = results.submissions;
                    bool allCompleted = true;

                    foreach (var result in submissionsResponse)
                    {
                        if (result == null)
                        {
                            Console.WriteLine("[ERROR] Null result in API response.");
                            continue;
                        }

                        string token = result.token;
                        int statusId = result.status_id;
                        Console.WriteLine($"[DEBUG] Token {token} Status: {statusId}");

                        if (statusId != 3) // Status 3 = Completed execution
                        {
                            allCompleted = false;
                            continue; // Skip unfinished submissions
                        }

                        string output = result.stdout?.ToString().Trim() ?? "No Output";
                        string error = result.stderr?.ToString().Trim() ?? "";
                        string compileError = result.compile_output?.ToString().Trim() ?? "";

                        string finalOutput = !string.IsNullOrEmpty(error) ? $"Error: {error}" :
                                             !string.IsNullOrEmpty(compileError) ? $"Compile Error: {compileError}" :
                                             output;

                        if (!string.IsNullOrEmpty(token) && tokenToInputMap.ContainsKey(token))
                        {
                            string input = tokenToInputMap[token]; // Get the original input
                            executionResults[input] = finalOutput; // Use input as the key
                        }
                        else
                        {
                            Console.WriteLine($"[ERROR] Token {token} not found in mapping!");
                        }
                    }

                    if (allCompleted) break; // Exit loop early if all submissions are done
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EXCEPTION] FetchExecutionResults Error: {ex.Message}");
                }

                retryCount++;
            }

            return executionResults;
        }


        private int GetJudge0LanguageId(string language) => language.ToLower() switch
        {
            "python" => 71,
            "java" => 91,
            "c++" => 54,
            "c#" => 51,
            _ => -1
        };
    }
}