using DotNetEnv;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using VideoIndexingARMAccounts.VideoIndexerClient.Model;
using DotNetEnv;
// ReSharper disable All

namespace VideoIndexingARMAccounts
{
    public static class Consts
    {
        public const string ApiVersion = "2024-01-01";
        public const string AzureResourceManager = "https://management.azure.com";
        public static readonly string SubscriptionId = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
        public static readonly string ResourceGroup = "MediaServer";
        public static readonly string ViAccountName = "Media123";
        public static readonly string ApiEndpoint = "https://api.videoindexer.ai";

        public static bool Valid() => !string.IsNullOrWhiteSpace(SubscriptionId) &&
                               !string.IsNullOrWhiteSpace(ResourceGroup) &&
                               !string.IsNullOrWhiteSpace(ViAccountName);
    }

    public class Program
    {
        //Choose public Access Video URL or File Path
        private const string VideoUrl = "https://penny123.blob.core.windows.net/4d0xjentm6/testredaction.mp4?skoid=55c92973-1b29-49d9-9380-93f41b9b9266&sktid=a5d97973-921d-42e2-9a4d-f6c40013c3bf&skt=2024-09-09T01%3A34%3A08Z&ske=2024-09-16T01%3A34%3A08Z&sks=b&skv=2021-10-04&sv=2021-10-04&st=2024-09-11T04%3A11%3A48Z&se=2024-09-11T05%3A16%3A48Z&sr=b&sp=r&sig=iOVx69d%2BKIOzExbUKJhWhpjMHU9EDO09ofI5S3C0xaQ%3D";
        //OR 
        private const string LocalVideoPath = "C:/Users/penny.kuo/Downloads/333.mp4";
        
        // Enter a list seperated by a comma of the AIs you would like to exclude in the format "<Faces,Labels,Emotions,ObservedPeople>". Leave empty if you do not want to exclude any AIs. For more see here https://api-portal.videoindexer.ai/api-details#api=Operations&operation=Upload-Video:~:text=AI%20to%20exclude%20when%20indexing%2C%20for%20example%20for%20sensitive%20scenarios.%20Options%20are%3A%20Face/Observed%20peopleEmotions/Labels%7D.
        private const string ExcludedAI = ""; 

        public static async Task Main(string[] args)
        {
            Env.Load();

            Console.WriteLine("Video Indexer API Samples ");
            Console.WriteLine("=========================== ");

            if (!Consts.Valid())
            {
                throw new Exception("Please Fill In SubscriptionId, Account Name and Resource Group on the Constant Class !");
            }
            
            // Create Video Indexer Client
            var client = new VideoIndexerClient.VideoIndexerClient();
            //Get Access Tokens
            await client.AuthenticateAsync();

            //1. Sample 1 : Get account details, not required in most cases
            Console.WriteLine("Sample1- Get Account Basic Details");
            await client.GetAccountAsync(Consts.ViAccountName);

            //2. Sample 2 :  Upload a video , do not wait for the index operation to complete. 
            Console.WriteLine("Sample2- Index a Video from URL");
            var videoId = await client.UploadUrlAsync(VideoUrl, "my-video-name", ExcludedAI, false);
            //var videoId = "";
            //2A.Sample 2A: Upload From Local File
            //if (File.Exists(LocalVideoPath))
            //{
            //    Console.WriteLine("Sample 2A - Index a video From File");
            //    var fileVideoId = await client.FileUploadAsync("my-other-video-name", LocalVideoPath);
            //    videoId = fileVideoId;
            //    Console.WriteLine($"Video ID to wait for indexing: {videoId}");
            //}
            // Sample 3 : Wait for the video index to finish ( Polling method)
            Console.WriteLine("Sample 3 - Polling on Video Completion Event");
            await client.WaitForIndexAsync(videoId);

            //Sample 4: Search for the video and get insights
            //Console.WriteLine("Sample 4 - Search for Video And get insights");
            //await client.GetVideoAsync(videoId);

            Console.WriteLine("!!!Sample RedactFaces !!!");
            await client.RedactFacesAsync(videoId);

            //// Sample 5: Widgets API's
            //Console.WriteLine("Sample 5- Widgets API");
            //await client.GetInsightsWidgetUrlAsync(videoId);
            //await client.GetPlayerWidgetUrlAsync(videoId);
            


            Console.WriteLine("\nPress Enter to exit...");
            var line = Console.ReadLine();
            if (line == "enter")
            {
                System.Environment.Exit(0);
            }
        }

    }
}
