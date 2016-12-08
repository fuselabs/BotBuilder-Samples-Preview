﻿namespace Search.Utilities
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public static partial class LUISTools
    {
        // Rest API primitives

        public static async Task<JObject> GetModelAsync(string subscriptionKey, string appID, CancellationToken ct)
        {
            JObject result = null;
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            var uri = $"https://api.projectoxford.ai/luis/v1.0/prog/apps/{appID}";
            var response = await client.GetAsync(uri, ct);
            if (response.IsSuccessStatusCode)
            {
                result = JObject.Parse(await response.Content.ReadAsStringAsync());
            }
            return result;
        }

        /// <summary>
        /// Get all of the LUIS apps for a given subscription.
        /// </summary>
        /// <param name="subscriptionKey">LUIS subscription key.</param>
        /// <returns>JArray of app descriptions.</returns>
        public static async Task<JArray> GetAppsAsync(string subscriptionKey, CancellationToken ct)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            var uri = $"https://api.projectoxford.ai/luis/v1.0/prog/apps";
            var response = await client.GetAsync(uri, ct);
            JArray result = null;
            if (response.IsSuccessStatusCode)
            {
                result = JArray.Parse(await response.Content.ReadAsStringAsync());
            }
            return result;
        }

        /// <summary>
        /// Import a LUIS model.
        /// </summary>
        /// <param name="subscriptionKey">LUIS Subscription key.</param>
        /// <param name="appName">Name of app to upload.</param>
        /// <param name="model">LUIS model.</param>
        /// <returns>ID of uploaded model.</returns>
        public static async Task<string> ImportModelAsync(string subscriptionKey, string appName, JObject model, CancellationToken ct)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            var uri = $"https://api.projectoxford.ai/luis/v1.0/prog/apps/import?appName={appName}";
            HttpResponseMessage response;
            var byteData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(model));
            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response = await client.PostAsync(uri, content, ct);
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(response.ReasonPhrase);
            }
            var id = await response.Content.ReadAsStringAsync();
            return id.Replace("\"", "");
        }

        /// <summary>
        /// Delete an existing LUIS application.
        /// </summary>
        /// <param name="subscriptionKey">LUIS Subscription key.</param>
        /// <param name="appID">ID of the app to delete.</param>
        /// <returns>True if model was deleted.</returns>
        public static async Task<bool> DeleteModelAsync(string subscriptionKey, string appID, CancellationToken ct)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            var uri = $"https://api.projectoxford.ai/luis/v1.0/prog/apps/{appID}";
            var response = await client.DeleteAsync(uri, ct);
            return response.IsSuccessStatusCode;
        }

        public enum TrainingStatus
        {
            Success = 0,
            Fail = 1,
            UpToDate = 2,
            InProgress = 3,
        };

        public static async Task<bool> TrainModelAsync(string subscriptionKey, string appID, CancellationToken ct)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            var uri = $"https://api.projectoxford.ai/luis/v1.0/prog/apps/{appID}/train";
            HttpResponseMessage response;
            byte[] byteData = Encoding.UTF8.GetBytes("{body}");
            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response = await client.PostAsync(uri, content, ct);
            }
            if (response.IsSuccessStatusCode)
            {
                bool isTrained = false;
                do
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    var a = JArray.Parse(await (await client.GetAsync(uri)).Content.ReadAsStringAsync());
                    isTrained = true;
                    foreach (dynamic model in a)
                    {
                        var status = model.Details.StatusId;
                        if (status == TrainingStatus.Fail)
                        {
                            throw new Exception(model.Details.FailureReason);
                        }
                        else if (status == TrainingStatus.InProgress)
                        {
                            isTrained = false;
                            break;
                        }
                    }
                } while (!isTrained);
            }
            return response.IsSuccessStatusCode;
        }

        public static async Task<bool> PublishModelAsync(string subscriptionKey, string appID, CancellationToken ct)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            var uri = $"https://api.projectoxford.ai/luis/v1.0/prog/apps/{appID}/publish";
            var body =
                @"{
                ""BotFramework"": {
                    ""Enabled"": false,
                    ""AppId"": """",
                    ""SubscriptionKey"": """",
                    ""Endpoint"": """"
                },
                ""Slack"": {
                    ""Enabled"": false,
                    ""ClientId"": """",
                    ""ClientSecret"": """",
                    ""RedirectUri"": """"
                }
            }";
            byte[] byteData = Encoding.UTF8.GetBytes($"{body}");
            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                var response = await client.PostAsync(uri, content, ct);
                return response.IsSuccessStatusCode;
            }
        }

        public static async Task<JObject> DownloadModelAsync(string subscriptionKey, string appID, CancellationToken ct)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            var uri = $"https://api.projectoxford.ai/luis/v1.0/prog/apps/{appID}/export";
            var response = await client.GetAsync(uri, ct);
            JObject result = null;
            if (response.IsSuccessStatusCode)
            {
                result = JObject.Parse(await response.Content.ReadAsStringAsync());
            }
            return result;
        }

        // Derived methods over REST API primitives

        /// <summary>
        /// Return the model information on a LUIS app or null if not present.
        /// </summary>
        /// <param name="subscriptionKey">LUIS subscription key.</param>
        /// <param name="appName">Name of app.</param>
        /// <returns>Model information for app or null if not present.</returns>
        public static async Task<JObject> GetModelByNameAsync(string subscriptionKey, string appName, CancellationToken ct)
        {
            JObject model = null;
            var apps = await GetAppsAsync(subscriptionKey, ct);
            if (apps != null)
            {
                foreach (var app in apps)
                {
                    if (string.Compare((string)app["Name"], appName, true) == 0)
                    {
                        model = (JObject)app;
                        break;
                    }
                }
            }
            return model;
        }

        public static async Task<string> CreateModelAsync(string subscriptionKey, dynamic model, CancellationToken ct)
        {
            string modelID = null;
            string appName = (string)model.name;
            var old = await LUISTools.GetModelByNameAsync(subscriptionKey, appName, ct);
            if (old != null)
            {
                await LUISTools.DeleteModelAsync(subscriptionKey, (string)old["ID"], ct);
            }
            string id = null;
            try
            {
                id = await ImportModelAsync(subscriptionKey, appName, model, ct);
                if (id != null
                    && await TrainModelAsync(subscriptionKey, id, ct)
                    && await PublishModelAsync(subscriptionKey, id, ct))
                {
                    modelID = id;
                }
            }
            catch (Exception)
            {
                // Try to clean up non published model
                if (id != null)
                {
                    await LUISTools.DeleteModelAsync(subscriptionKey, id, ct);
                }
                throw;
            }
            return modelID;
        }

        /// <summary>
        /// Return the LUIS model ID of an existing app or import it from <paramref name="modelPath"/> and return the new ID.
        /// </summary>
        /// <param name="subscriptionKey">LUIS subscription key.</param>
        /// <param name="modelPath">Path to the exported LUIS model.</param>
        /// <returns>LUIS Model ID.</returns>
        public static async Task<string> GetOrCreateModelAsync(string subscriptionKey, string modelPath, CancellationToken ct)
        {
            string modelID = null;
            dynamic newModel = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(modelPath));
            var model = await GetModelByNameAsync(subscriptionKey, (string) newModel.name, ct);
            if (model == null)
            {
                modelID = await CreateModelAsync(subscriptionKey, newModel, ct);
            }
            else
            {
                modelID = (string)model["ID"];
            }
            return modelID;
        }
    }
}
