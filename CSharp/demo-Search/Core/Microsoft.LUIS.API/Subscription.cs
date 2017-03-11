using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.LUIS.API
{
    public class Subscription
    {
        private readonly string _domain;
        private readonly string _subscription;
        private HttpClient _client;

        public Subscription(string domain, string subscription)
        {
            _domain = domain;
            _subscription = subscription;
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _subscription);
        }

        public Uri BaseUri(string api)
        {
            return new Uri($"https://{_domain}/luis/api/v2.0/{api}");
        }

        public async Task<HttpResponseMessage> GetAsync(string api, CancellationToken ct)
        {
            var uri = BaseUri(api);
            return await _client.GetAsync(uri, ct);
        }

        public async Task<HttpResponseMessage> PostAsync(string api, JToken json, CancellationToken ct)
        {
            var uri = BaseUri(api);
            HttpResponseMessage response;
            var byteData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(json));
            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response = await _client.PostAsync(uri, content, ct);
            }
            return response;
        }

        public async Task<HttpResponseMessage> DeleteAsync(string api, CancellationToken ct)
        {
            var uri = BaseUri(api);
            return await _client.DeleteAsync(uri, ct);
        }

        /// <summary>
        /// Get all of the LUIS apps in subscription.
        /// </summary>
        /// <param name="subscription">LUIS subscription key.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>JArray of app descriptions.</returns>
        public async Task<JArray> GetAppsAsync(CancellationToken ct)
        {
            JArray result = null;
            var response = await GetAsync("apps", ct);
            if (response.IsSuccessStatusCode)
            {
                result = JArray.Parse(await response.Content.ReadAsStringAsync());
            }
            return result;
        }

        public async Task<Application> GetApplicationAsync(string appID, CancellationToken ct)
        {
            var response = await GetAsync($"apps/{appID}", ct);
            return response.IsSuccessStatusCode
                ? new Application(this, JObject.Parse(await response.Content.ReadAsStringAsync()))
                : null;
        }

        /// <summary>
        /// Get a model by name.
        /// </summary>
        /// <param name="appName">Name of model.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>New application or null if not present.</returns>
        public async Task<Application> GetApplicationByNameAsync(string appName, CancellationToken ct)
        {
            JObject model = null;
            var apps = await GetAppsAsync(ct);
            if (apps != null)
            {
                foreach (var app in apps)
                {
                    if (string.Compare((string)app["name"], appName, true) == 0)
                    {
                        model = (JObject)app;
                        break;
                    }
                }
            }
            return model == null ? null : new Application(this, model);
        }

        /// <summary>
        /// Import a LUIS model.
        /// </summary>
        /// <param name="appName">Name of app to upload.</param>
        /// <param name="model">LUIS model.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>ID of uploaded model.</returns>
        public async Task<Application> ImportApplicationAsync(string appName, JObject model, CancellationToken ct)
        {
            var response = await PostAsync($"apps/import?appName={appName}", model, ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(response.ReasonPhrase);
            }
            var id = await response.Content.ReadAsStringAsync();
            return await GetApplicationAsync(id.Replace("\"", ""), ct);
        }

        public async Task<Application> ReplaceApplicationAsync(dynamic model, CancellationToken ct)
        {
            Application newApp = null;
            string appName = (string)model.name;
            var old = await GetApplicationByNameAsync(appName, ct);
            if (old != null)
            {
                await old.DeleteAsync(ct);
            }
            Application app = null;
            try
            {
                app = await ImportApplicationAsync(appName, model, ct);
                if (app != null
                    && await app.TrainAsync(ct)
                    && await app.PublishAsync(false, ct))
                {
                    newApp = app;
                }
            }
            catch (Exception)
            {
                // Try to clean up non published model
                if (app != null)
                {
                    await app.DeleteAsync(ct);
                }
                throw;
            }
            return newApp;
        }

        /// <summary>
        /// Return the LUIS model ID of an existing app or import it from <paramref name="modelPath"/> and return the new ID.
        /// </summary>
        /// <param name="modelPath">Path to the exported LUIS model.</param>
        /// <returns>LUIS Model ID.</returns>
        public async Task<Application> GetOrCreateApplicationAsync(string modelPath, CancellationToken ct)
        {
            dynamic newModel = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(modelPath));
            var app = await GetApplicationByNameAsync((string)newModel.name, ct);
            if (app == null)
            {
                app = await ReplaceApplicationAsync(newModel, ct);
            }
            return app;
        }
    }
}
