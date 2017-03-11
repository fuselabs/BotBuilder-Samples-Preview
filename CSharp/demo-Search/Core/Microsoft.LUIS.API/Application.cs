using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.LUIS.API
{
    public class Application
    {
        private readonly Subscription _subscription;
        public readonly string ApplicationID;
        private readonly string _version;
        private readonly JObject _model;

        internal Application(Subscription subscription, JObject model)
        {
            _subscription = subscription;
            _model = model;
            ApplicationID = (string) model["id"];
            _version = "0.1";
        }

        public string AppAPI(string api)
        {
            return $"apps/{ApplicationID}/versions/{_version}/{api}";
        }

        public async Task<HttpResponseMessage> GetAsync(string api, CancellationToken ct)
        {
            return await _subscription.GetAsync(AppAPI(api), ct);
        }

        public async Task<HttpResponseMessage> PostAsync(string api, JToken json, CancellationToken ct)
        {
            return await _subscription.PostAsync(AppAPI(api), json, ct);
        }

        public async Task<HttpResponseMessage> DeleteAsync(string api, CancellationToken ct)
        {
            return await _subscription.DeleteAsync(AppAPI(api), ct);
        }

        /// <summary>
        /// Delete the application.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if application was deleted.</returns>
        public async Task<bool> DeleteAsync(CancellationToken ct)
        {
            var response = await _subscription.DeleteAsync($"apps/{ApplicationID}", ct);
            return response.IsSuccessStatusCode;
        }

        public enum TrainingStatus
        {
            Success = 0,
            Fail = 1,
            UpToDate = 2,
            InProgress = 3,
        };

        public async Task<bool> TrainAsync(CancellationToken ct)
        {
            var response = await PostAsync("train", new JObject(), ct);
            if (response.IsSuccessStatusCode)
            {
                bool isTrained = false;
                do
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    var a = JArray.Parse(await (await GetAsync("train", ct)).Content.ReadAsStringAsync());
                    isTrained = true;
                    foreach (dynamic model in a)
                    {
                        var status = model.details.statusId;
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

        public async Task<bool> PublishAsync(bool isStaging, CancellationToken ct)
        {
            var staging = isStaging ? "true" : "false";
            var body = JObject.Parse($"{{\"versionId\": \"{_version}\", \"isStaging\": {staging}}}");
            var response = await _subscription.PostAsync($"apps/{ApplicationID}/publish", body, ct);
            return response.IsSuccessStatusCode;
        }

        public async Task<JObject> DownloadAsync(CancellationToken ct)
        {
            var response = await GetAsync("export", ct);
            return response.IsSuccessStatusCode
                ? JObject.Parse(await response.Content.ReadAsStringAsync())
                : null;
        }

        /// <summary>
        /// Upload a batch of labelled utterances.
        /// </summary>
        /// <param name="utterances">Utterances to upload.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task<JArray> UploadUtterancesAsync(dynamic utterances, CancellationToken ct)
        {
            var response = await PostAsync("examples", utterances, ct);
            return response.IsSuccessStatusCode
                ? JArray.Parse(await response.Content.ReadAsStringAsync())
                : null;
        }

        public async Task<JArray> GetIntentsAsync(CancellationToken ct, int? skip, int? take)
        {
            var uri = "intents";
            if (skip.HasValue)
            {
                uri += $"?{skip.Value}";
            }
            if (take.HasValue)
            {
                uri += $"&{take.Value}";
            }
            var response = await GetAsync(uri, ct);
            return response.IsSuccessStatusCode
                ? JArray.Parse(await response.Content.ReadAsStringAsync())
                : null;
        }

        /* public async Task<bool> CreateIntentAsync(string intent, CancellationToken ct)
        {
        }
        */
    }
}
