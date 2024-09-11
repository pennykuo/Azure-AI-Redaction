using Azure.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using VideoIndexingARMAccounts.VideoIndexerClient.Auth;
using VideoIndexingARMAccounts.VideoIndexerClient.Model;
using VideoIndexingARMAccounts.VideoIndexerClient.Utils;
using static VideoIndexingARMAccounts.Consts;

namespace VideoIndexingARMAccounts.VideoIndexerClient
{
    public class VideoIndexerClient
    {
        private readonly HttpClient _httpClient;
        private string _armAccessToken;
        private string _accountAccessToken;
        private Account _account;
        private JobStatus _jobStatus;
        private object jobId;
        private readonly TimeSpan _pollingInteval = TimeSpan.FromSeconds(10);

        public VideoIndexerClient()
        {
            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;
            _httpClient = HttpClientUtils.CreateHttpClient();

        }

        public async Task AuthenticateAsync()
        {
            try
            {
                _armAccessToken = await AccountTokenProvider.GetArmAccessTokenAsync();
                _accountAccessToken = await AccountTokenProvider.GetAccountAccessTokenAsync(_armAccessToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Get Information about the Account
        /// </summary>
        /// <param name="accountName"></param>
        /// <returns></returns>
        /// 

        public async Task<string> RedactFacesAsync(string videoId)
        {
            var queryParams = new Dictionary<string, string>()
            {   { "priority", "Low" },
                { "name", "testredaction"},
                { "privacy", "private" },
                { "streamingPreset", "Default" },
                { "description", "video_description" },
                { "accessToken" , _accountAccessToken }
            }.CreateQueryString();
            var requestUrl = $"{ApiEndpoint}/{_account.Location}/Accounts/{_account.Properties.Id}/Videos/{videoId}/redact?{queryParams}";

            var requestBody = new
            {
                faces = new
                {
                    blurringKind = "BoundingBox"
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(requestUrl, jsonContent);

            Console.WriteLine(response.Headers.ToString());

            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                string redirectUrl = await PollJobStatusAsync(response);

                if (!string.IsNullOrEmpty(redirectUrl))
                {
                    string filePath = "./filename.mp4";

                    await GetDownloadUrlAsync(redirectUrl, filePath);

                    //await DownloadFileAsync(downloadUrl, filePath);
                    return null;
                }
            }
            else
            {
                Console.WriteLine($"Redaction request failed with status code: {response.StatusCode}");
            }

            return null;
        }

        private async Task<string> PollJobStatusAsync(HttpResponseMessage response)
        {
            while (true)
            {
                var locationUrl = response.Headers.Location?.ToString();
                //Console.WriteLine($"Location URL: {locationUrl}");

                if (!string.IsNullOrEmpty(locationUrl))
                {
                    var jobId = locationUrl.Split('/').Last();
                    //Console.WriteLine($"Extracted Job ID: {jobId}");

                    var statusUrl = $"{ApiEndpoint}/{_account.Location}/Accounts/{_account.Properties.Id}/Jobs/{jobId}?accessToken={_accountAccessToken}";
                    var statusResponse = await _httpClient.GetAsync(statusUrl);

                    if (statusResponse.IsSuccessStatusCode)
                    {
                        var statusContent = await statusResponse.Content.ReadAsStringAsync();
                        _jobStatus = JsonSerializer.Deserialize<JobStatus>(statusContent);

                        Console.WriteLine($"Job Status: {_jobStatus.State}, Progress: {_jobStatus.Progress}%");

                        

                        await Task.Delay(5000); // 等待5秒后重试
                    }
                    else
                    {
                        Console.WriteLine($"Job Status: {_jobStatus.State}, Progress:100%");

                        if (statusResponse.StatusCode == HttpStatusCode.RedirectMethod)
                        {

                            var redirectStatusUrl = $"{ApiEndpoint}/{_account.Location}/Accounts/{_account.Properties.Id}/Jobs/{jobId}?accessToken={_accountAccessToken}";
                            var redirectResponse = await _httpClient.GetAsync(redirectStatusUrl);

                            //if (redirectResponse.StatusCode == HttpStatusCode.SeeOther || redirectResponse.StatusCode == HttpStatusCode.TemporaryRedirect)
                            //{
                            string redirectUrl = redirectResponse.Headers.Location?.ToString();

                            Console.WriteLine($"Redirect URL: {redirectUrl}");

                            //return $"{redirectUrl}?accessToken={_accountAccessToken}";
                            return $"{redirectUrl}";

                            //}
                        }
                        return null;
                    }
                }
                else
                {
                    Console.WriteLine($"沒有進入while的第一個判斷是");

                    
                }
            }
        }
        public async Task GetDownloadUrlAsync(string redirectUrl, string filePath)
        {
            try
            {
                // 設置授權標頭
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accountAccessToken);
                Console.WriteLine($"Token: {_accountAccessToken}");

                // 獲取下載 URL
                var downloadResponse = await _httpClient.GetAsync(redirectUrl);
                if (!downloadResponse.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get download URL with status code: {downloadResponse.StatusCode}");
                }

                var downloadUrl = await downloadResponse.Content.ReadAsStringAsync();
                downloadUrl = downloadUrl.Trim('"'); // 清理 URL 字符

                Console.WriteLine($"Cleaned Download URL: {downloadUrl}");
                _httpClient.DefaultRequestHeaders.Authorization = null;

                // 下載文件
                var fileResponse = await _httpClient.GetAsync(downloadUrl);
                if (!fileResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to download file. Status code: {fileResponse.StatusCode}");
                    var responseContent = await fileResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Response content: {responseContent}");
                    return;
                }

                var fileBytes = await fileResponse.Content.ReadAsByteArrayAsync();
                Console.WriteLine($"fileBytes length: {fileBytes.Length}");

                await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

                Console.WriteLine($"File downloaded successfully to: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while downloading the file: {ex.Message}");
                throw; // Re-throw the exception to handle it at a higher level if needed
            }
        }
        public async Task DownloadFileAsync(string url, string filePath)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to download file. Status code: {response.StatusCode}");
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Response content: {responseContent}");
                    return;
                }
                var fileBytes = await response.Content.ReadAsByteArrayAsync();
                await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);
                Console.WriteLine($"File downloaded successfully to: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while downloading the file: {ex.Message}");
                throw;
            }
        }
        public async Task<Account> GetAccountAsync(string accountName)
        {
            if (_account != null)
            {
                return _account;
            }
            Console.WriteLine($"Getting account {accountName}.");
            try
            {
                // Set request uri
                var requestUri = $"{AzureResourceManager}/subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroup}/providers/Microsoft.VideoIndexer/accounts/{accountName}?api-version={ApiVersion}";
                var client = HttpClientUtils.CreateHttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _armAccessToken);

                var result = await client.GetAsync(requestUri);

                result.VerifyStatus(System.Net.HttpStatusCode.OK);
                var jsonResponseBody = await result.Content.ReadAsStringAsync();
                var account = JsonSerializer.Deserialize<Account>(jsonResponseBody);
                VerifyValidAccount(account, accountName);
                Console.WriteLine($"[Account Details] Id:{account.Properties.Id}, Location: {account.Location}");
                _account = account;
                return account;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw;
            }
        }

        /// <summary>
        /// Uploads a video and starts the video index. Calls the uploadVideo API (https://api-portal.videoindexer.ai/api-details#api=Operations&operation=Upload-Video)
        /// </summary>
        /// <param name="videoUrl"> Link To Publicy Accessed Video URL</param>
        /// <param name="videoName"> The Asset name to be used </param>
        /// <param name="exludedAIs"> The ExcludeAI list to run </param>
        /// <param name="waitForIndex"> should this method wait for index operation to complete </param>
        /// <exception cref="Exception"></exception>
        /// <returns> Video Id of the video being indexed, otherwise throws excpetion</returns>
        public async Task<string> UploadUrlAsync(string videoUrl, string videoName, string exludedAIs = null, bool waitForIndex = false)
        {
            if (_account == null)
            {
                throw new Exception("Call Get Account Details First");
            }

            Console.WriteLine($"Video for account {_account.Properties.Id} is starting to upload.");

            try
            {
                //Build Query Parameter Dictionary
                var queryDictionary = new Dictionary<string, string>
                {
                    { "name", videoName },
                    { "description", "video_description" },
                    { "privacy", "private" },
                    { "accessToken" , _accountAccessToken },
                    { "videoUrl" , videoUrl }
                };

                if (!Uri.IsWellFormedUriString(videoUrl, UriKind.Absolute))
                {
                    throw new ArgumentException("VideoUrl or LocalVidePath are invalid");
                }

                var queryParams = queryDictionary.CreateQueryString();
                if (!string.IsNullOrEmpty(exludedAIs))
                    queryParams += AddExcludedAIs(exludedAIs);

                // Send POST request
                var url = $"{ApiEndpoint}/{_account.Location}/Accounts/{_account.Properties.Id}/Videos?{queryParams}";
                var uploadRequestResult = await _httpClient.PostAsync(url, null);
                uploadRequestResult.VerifyStatus(System.Net.HttpStatusCode.OK);
                var uploadResult = await uploadRequestResult.Content.ReadAsStringAsync();

                // Get the video ID from the upload result
                var videoId = JsonSerializer.Deserialize<Video>(uploadResult).Id;
                Console.WriteLine($"Video ID {videoId} was uploaded successfully");

                if (waitForIndex)
                {
                    Console.WriteLine("Waiting for Index Operation to Complete");
                    await WaitForIndexAsync(videoId);
                }
                return videoId;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw;
            }
        }

        /// <summary>
        /// Calls getVideoIndex API in 10 second intervals until the indexing state is 'processed'(https://api-portal.videoindexer.ai/api-details#api=Operations&operation=Get-Video-Index)
        /// </summary>
        /// <param name="videoId"> The video id </param>
        /// <exception cref="Exception"></exception>
        /// <returns> Prints video index when the index is complete, otherwise throws exception </returns>
        public async Task WaitForIndexAsync(string videoId)
        {
            Console.WriteLine($"Waiting for video {videoId} to finish indexing.");
            while (true)
            {
                var queryParams = new Dictionary<string, string>()
                {
                    {"language", "English"},
                    { "accessToken" , _accountAccessToken },
                }.CreateQueryString();

                var requestUrl = $"{ApiEndpoint}/{_account.Location}/Accounts/{_account.Properties.Id}/Videos/{videoId}/Index?{queryParams}";
                var videoGetIndexRequestResult = await _httpClient.GetAsync(requestUrl);
                videoGetIndexRequestResult.VerifyStatus(System.Net.HttpStatusCode.OK);
                var videoGetIndexResult = await videoGetIndexRequestResult.Content.ReadAsStringAsync();
                var processingState = JsonSerializer.Deserialize<Video>(videoGetIndexResult).State;

                // If job is finished
                if (processingState == ProcessingState.Processed.ToString())
                {
                    Console.WriteLine($"The video index has completed for video ID {videoId}.");
                    //Console.WriteLine($"The video index has completed. Here is the full JSON of the index for video ID {videoId}: \n{videoGetIndexResult}");
                    return;
                }
                else if (processingState == ProcessingState.Failed.ToString())
                {
                    Console.WriteLine($"The video index failed for video ID {videoId}.");
                    throw new Exception(videoGetIndexResult);
                }

                // Job hasn't finished
                Console.WriteLine($"The video index state is {processingState}");
                await Task.Delay(_pollingInteval);
            }
        }

        /// <summary>
        /// Searches for the video in the account. Calls the searchVideo API (https://api-portal.videoindexer.ai/api-details#api=Operations&operation=Search-Videos)
        /// </summary>
        /// <param name="videoId"> The video id </param>
        /// <returns> Prints the video metadata, otherwise throws excpetion</returns>
        public async Task GetVideoAsync(string videoId)
        {
            Console.WriteLine($"Searching videos in account {_account.Properties.Id} for video ID {videoId}.");
            var queryParams = new Dictionary<string, string>()
            {
                {"id", videoId},
                { "accessToken" , _accountAccessToken },
            }.CreateQueryString();

            try
            {
                var requestUrl = $"{ApiEndpoint}/{_account.Location}/Accounts/{_account.Properties.Id}/Videos/Search?{queryParams}";
                var searchRequestResult = await _httpClient.GetAsync(requestUrl);
                searchRequestResult.VerifyStatus(System.Net.HttpStatusCode.OK);
                var searchResult = await searchRequestResult.Content.ReadAsStringAsync();
                Console.WriteLine($"Here are the search results: \n{searchResult}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public async Task<string> FileUploadAsync(string videoName, string mediaPath, string exludedAIs = null)
        {
            if (!File.Exists(mediaPath))
                throw new Exception($"Could not find file at path {mediaPath}");

            var queryParams = new Dictionary<string, string>
            {
                { "name", videoName },
                { "description", "video_description" },
                { "privacy", "private" },
                { "accessToken" , _accountAccessToken },
                { "partition", "partition" }
            }.CreateQueryString();

            if (!string.IsNullOrEmpty(exludedAIs))
                queryParams += AddExcludedAIs(exludedAIs);

            var url = $"{ApiEndpoint}/{_account.Location}/Accounts/{_account.Properties.Id}/Videos?{queryParams}";
            // Create multipart form data content
            using var content = new MultipartFormDataContent();
            // Add file content
            await using var fileStream = new FileStream(mediaPath, FileMode.Open, FileAccess.Read);
            using var streamContent = new StreamContent(fileStream);
            content.Add(streamContent, "fileName", Path.GetFileName(mediaPath));
            Console.WriteLine("Uploading a local file using multipart/form-data post request..");
            // Send POST request
            var response = await _httpClient.PostAsync(url, content);
            Console.WriteLine(response.Headers.ToString());
            // Process response
            //////////////////////////////
            response.VerifyStatus(System.Net.HttpStatusCode.OK);
            var uploadResult = await response.Content.ReadAsStringAsync();

            // Get the video ID from the upload result
            var videoId = JsonSerializer.Deserialize<Video>(uploadResult).Id;
            Console.WriteLine($"--------------------Video ID {videoId} was uploaded successfully------------------");

            return videoId;
            /////////////////////////////////////////////////
            //if (response.IsSuccessStatusCode)
            //{
            //    var responseBody = await response.Content.ReadAsStringAsync();
            //    return responseBody;
            //}
            //Console.WriteLine($"Request failed with status code: {response.StatusCode}");
            //return response.ToString();
        }

        /// <summary>
        /// Calls the getVideoInsightsWidget API (https://api-portal.videoindexer.ai/api-details#api=Operations&operation=Get-Video-Insights-Widget)
        /// </summary>
        /// <param name="videoId"> The video id </param>
        /// <returns> Prints the VideoInsightsWidget URL, otherwise throws exception</returns>
        public async Task GetInsightsWidgetUrlAsync(string videoId)
        {
            Console.WriteLine($"Getting the insights widget URL for video {videoId}");
            var queryParams = new Dictionary<string, string>()
            {
                {"widgetType", "Keywords"},
                { "accessToken" , _accountAccessToken },
                {"allowEdit", "true"},
            }.CreateQueryString();
            try
            {
                var requestUrl = $"{ApiEndpoint}/{_account.Location}/Accounts/{_account.Properties.Id}/Videos/{videoId}/InsightsWidget?{queryParams}";
                var insightsWidgetRequestResult = await _httpClient.GetAsync(requestUrl);
                insightsWidgetRequestResult.VerifyStatus(System.Net.HttpStatusCode.MovedPermanently);
                var insightsWidgetLink = insightsWidgetRequestResult.Headers.Location;
                Console.WriteLine($"Got the insights widget URL: \n{insightsWidgetLink}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        /// <summary>
        /// Calls the getVideoPlayerWidget API (https://api-portal.videoindexer.ai/api-details#api=Operations&operation=Get-Video-Player-Widget)
        /// </summary>
        /// <param name="videoId"> The video id </param>
        /// <returns> Prints the VideoPlayerWidget URL, otherwise throws exception</returns>
        public async Task GetPlayerWidgetUrlAsync(string videoId)
        {
            Console.WriteLine($"Getting the player widget URL for video {videoId}");

            try
            {
                var requestUrl = $"{ApiEndpoint}/{_account.Location}/Accounts/{_account.Properties.Id}/Videos/{videoId}/PlayerWidget";
                var playerWidgetRequestResult = await _httpClient.GetAsync(requestUrl);

                var playerWidgetLink = playerWidgetRequestResult.Headers.Location;
                playerWidgetRequestResult.VerifyStatus(System.Net.HttpStatusCode.MovedPermanently);
                Console.WriteLine($"Got the player widget URL: \n{playerWidgetLink}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private string AddExcludedAIs(string ExcludedAI)
        {
            if (string.IsNullOrEmpty(ExcludedAI))
            {
                return "";
            }
            var list = ExcludedAI.Split(',');
            return list.Aggregate("", (current, item) => current + ("&ExcludedAI=" + item));
        }

        private static void VerifyValidAccount(Account account, string accountName)
        {
            if (string.IsNullOrWhiteSpace(account.Location) || account.Properties == null || string.IsNullOrWhiteSpace(account.Properties.Id))
            {
                Console.WriteLine($"{nameof(accountName)} {accountName} not found. Check {nameof(SubscriptionId)}, {nameof(ResourceGroup)}, {nameof(accountName)} ar valid.");
                throw new Exception($"Account {accountName} not found.");
            }
        }

    }
}
