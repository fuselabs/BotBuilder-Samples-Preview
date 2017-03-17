using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.LUIS.API
{
    public class Subscription
    {
        public readonly string Domain;
        public readonly string Key;
        public HttpClient Client;

        public Subscription(string domain, string subscription)
        {
            this.Domain = domain;
            Key = subscription;
            Client = new HttpClient();
            Client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", Key);
        }

        public Uri BaseUri(string api)
        {
            return new Uri($"https://{Domain}/luis/api/v2.0/{api}");
        }

        public async Task<HttpResponseMessage> GetAsync(string api, CancellationToken ct)
        {
            var uri = BaseUri(api);
            return await Client.GetAsync(uri, ct);
        }

        public async Task<HttpResponseMessage> PostAsync(string api, JToken json, CancellationToken ct)
        {
            var uri = BaseUri(api);
            HttpResponseMessage response;
            var byteData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(json));
            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response = await Client.PostAsync(uri, content, ct);
            }
            return response;
        }

        public async Task<HttpResponseMessage> DeleteAsync(string api, CancellationToken ct)
        {
            var uri = BaseUri(api);
            return await Client.DeleteAsync(uri, ct);
        }

        private IEnumerablePage<T> EnumerablePage<T>(string api, CancellationToken ct, int? take = null)
        {
            return new EnumerableAsync<T>(async (skip) =>
            {
                ICollection<T> result = null;
                var uri = $"{api}?skip={skip}";
                if (take.HasValue)
                {
                    uri += $"&take={take}";
                }
                var response = await GetAsync(uri, ct);
                if (response.IsSuccessStatusCode)
                {
                    var arr = JArray.Parse(await response.Content.ReadAsStringAsync());
                    if (arr.Count > 0)
                    {
                        result = (ICollection<T>)arr.Values<T>().ToList();
                    }
                }
                return result;
            }, ct);
        }

        /// <summary>
        /// Get all of the LUIS apps in subscription.
        /// </summary>
        /// <param name="subscription">LUIS subscription key.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>IEnumerablePage of app descriptions.</returns>
        public IEnumerablePage<JObject> GetApps(CancellationToken ct, int? take = null)
        {
            return EnumerablePage<JObject>("apps", ct, take);
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
            var model = await GetApps(ct).FirstAsync((app) => string.Compare((string)app["name"], appName, true) == 0);
            return model == null ? null : new Application(this, model);
        }

        public async Task<bool> DeleteApplicationByNameAsync(string appName, CancellationToken ct)
        {
            bool deleted = false;
            var app = await GetApplicationByNameAsync(appName, ct);
            if (app != null)
            {
                deleted = await app.DeleteAsync(ct);
            }
            return deleted;
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
        /// Return the LUIS application of an existing app or import it from <paramref name="modelPath"/> and return a new application.
        /// </summary>
        /// <param name="modelPath">Path to the exported LUIS model.</param>
        /// <returns>LUIS Model ID.</returns>
        public async Task<Application> GetOrImportApplicationAsync(string modelPath, CancellationToken ct)
        {
            dynamic newModel = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(modelPath));
            var app = await GetApplicationByNameAsync((string)newModel.name, ct);
            if (app == null)
            {
                app = await ReplaceApplicationAsync(newModel, ct);
            }
            return app;
        }

        public async Task<Application> GetOrCreateApplicationAsync(string name, string culture, CancellationToken ct)
        {
            var app = await GetApplicationByNameAsync(name, ct);
            if (app == null)
            {
                var appDesc = new JObject();
                appDesc["name"] = name;
                appDesc["culture"] = culture;
                var response = await PostAsync("apps", appDesc, ct);
                if (response.IsSuccessStatusCode)
                {
                    var id = (await response.Content.ReadAsStringAsync()).Replace("\"", string.Empty);
                    app = await GetApplicationAsync(id, ct);
                }
            }
            return app;
        }
    }
}
