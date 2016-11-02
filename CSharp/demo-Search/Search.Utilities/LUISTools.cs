namespace Search.Utilities
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;

    public static partial class LUISTools
    {
        // Rest API primitives

        public static async Task<JObject> GetModelAsync(string subscriptionKey, string appID)
        {
            JObject result = null;
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            var uri = $"https://api.projectoxford.ai/luis/v1.0/prog/apps/{appID}";
            var response = await client.GetAsync(uri);
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
        public static async Task<JArray> GetAppsAsync(string subscriptionKey)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            var uri = $"https://api.projectoxford.ai/luis/v1.0/prog/apps";
            var response = await client.GetAsync(uri);
            JArray result = null;
            if (response.IsSuccessStatusCode)
            {
                result = JArray.Parse(await response.Content.ReadAsStringAsync());
            }
            return result;
        }

        /// <summary>
        /// Import a LUIS model found in a JSON file.
        /// </summary>
        /// <param name="subscriptionKey">LUIS Subscription key.</param>
        /// <param name="appName">Name of app to upload.</param>
        /// <param name="modelPath">Path to JSON model.</param>
        /// <returns>ID of uploaded model.</returns>
        public static async Task<string> ImportModelAsync(string subscriptionKey, string appName, string modelPath)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            var uri = $"https://api.projectoxford.ai/luis/v1.0/prog/apps/import?appName={appName}";
            HttpResponseMessage response;
            var byteData = Encoding.UTF8.GetBytes(System.IO.File.ReadAllText(modelPath));
            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response = await client.PostAsync(uri, content);
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(await response.Content.ReadAsStringAsync());
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
        public static async Task<bool> DeleteModelAsync(string subscriptionKey, string appID)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            var uri = $"https://api.projectoxford.ai/luis/v1.0/prog/apps/{appID}";
            var response = await client.DeleteAsync(uri);
            return response.IsSuccessStatusCode;
        }

        public static async Task<bool> TrainModelAsync(string subscriptionKey, string appID)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            var uri = $"https://api.projectoxford.ai/luis/v1.0/prog/apps/{appID}/train";
            HttpResponseMessage response;
            byte[] byteData = Encoding.UTF8.GetBytes("{body}");
            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response = await client.PostAsync(uri, content);
            }
            if (response.IsSuccessStatusCode)
            {
                JObject model;
                do
                {
                    System.Threading.Thread.Sleep(1000);
                    // TODO: var a = JArray.Parse(await (await client.GetAsync(uri)).Content.ReadAsStringAsync());
                    model = await GetModelAsync(subscriptionKey, appID);
                } while (!(bool)model["IsTrained"]);
            }
            return response.IsSuccessStatusCode;
        }

        public static async Task<bool> PublishModelAsync(string subscriptionKey, string appID)
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
                var response = await client.PostAsync(uri, content);
                return response.IsSuccessStatusCode;
            }
        }

        // Derived methods over REST API primitives

        /// <summary>
        /// Return the model information on a LUIS app or null if not present.
        /// </summary>
        /// <param name="subscriptionKey">LUIS subscription key.</param>
        /// <param name="appName">Name of app.</param>
        /// <returns>Model information for app or null if not present.</returns>
        public static async Task<JObject> GetModelByNameAsync(string subscriptionKey, string appName)
        {
            JObject model = null;
            var apps = await GetAppsAsync(subscriptionKey);
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

        public static async Task<string> CreateModelAsync(string subscriptionKey, string appName, string modelPath)
        {
            string modelID = null;
            var old = await LUISTools.GetModelByNameAsync(subscriptionKey, appName);
            if (old != null)
            {
                await LUISTools.DeleteModelAsync(subscriptionKey, (string)old["ID"]);
            }
            string id = null;
            try
            {
                id = await ImportModelAsync(subscriptionKey, appName, modelPath);
                if (id != null
                    && await TrainModelAsync(subscriptionKey, id)
                    && await PublishModelAsync(subscriptionKey, id))
                {
                    modelID = id;
                }
            }
            catch (Exception)
            {
                // Try to clean up non published model
                if (id != null)
                {
                    await LUISTools.DeleteModelAsync(subscriptionKey, id);
                }
                throw;
            }
            return modelID;
        }

        /// <summary>
        /// Return the LUIS model ID of an existing app or import it from <paramref name="modelPath"/> and return the new ID.
        /// </summary>
        /// <param name="subscriptionKey">LUIS subscription key.</param>
        /// <param name="appName">Name of app.</param>
        /// <param name="modelPath">Path to the exported LUIS model.</param>
        /// <returns>LUIS Model ID.</returns>
        public static async Task<string> GetOrCreateModelAsync(string subscriptionKey, string appName, string modelPath)
        {
            string modelID = null;
            var model = await GetModelByNameAsync(subscriptionKey, appName);
            if (model == null)
            {
                modelID = await CreateModelAsync(subscriptionKey, appName, modelPath);
            }
            else
            {
                modelID = (string)model["ID"];
            }
            return modelID;
        }
    }
}
