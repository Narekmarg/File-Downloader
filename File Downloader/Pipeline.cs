using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Threading.Tasks.Dataflow;
using System.Net;
using System.Net.Http;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;
using System.Collections.Concurrent;

namespace File_Downloader
{
    public class Pipeline
    {

        public Action<double> UpdateProgress;
        public Action<double> UpdateConversionProgress;

        private int fileCount = 0;
        private int convertedFilesCount = 0;
        private int currentFileCount = 0;
        private TransformManyBlock<string, string> getFileUrls;
        public string FolderPath { get; set; }
        public string ConvertedFilesFolder { get; set; }

        public BufferBlock<List<Citizen>> citizensOfArmenia {get; private set;}

        public void BuildPipeline(CancellationToken token)
        {
                // create the blocks
                getFileUrls = new TransformManyBlock<string, string>(
                (url) =>
                {
                    List<string> fileUrls = new List<string>();
                    GetFileUrls(url, fileUrls);
                    return fileUrls;
                }
                , new ExecutionDataflowBlockOptions { CancellationToken = token }
                );

            var downloadFile = new TransformBlock<string, string>(
                 (fileUrl) =>
                 {
                     string fileName = Regex.Match(fileUrl, @"([^/]+$)").Value;
                     using (var client = new WebClient())
                     {
                         client.DownloadFile(fileUrl, FolderPath + "\\" + fileName);
                     }
                     Interlocked.Increment(ref currentFileCount);
                     UpdateProgress((double)currentFileCount / (double)fileCount);
                     return FolderPath + "\\" + fileName;
                 }
                ,
                new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = token,
                }
                );

            var convertFile = new TransformBlock<string, string>(
                (fileName) =>
                {
                    string newFileName = ConvertedFilesFolder + @"\" + Regex.Match(fileName, @"([^\\]+$)").Value + "x";
                    Process process = new Process();
                    process.StartInfo.Arguments = string.Format(@" -nme -oice {0} {1}", fileName, newFileName);
                    process.StartInfo.FileName = @"c:\Program Files (x86)\Microsoft Office\Office12\excelcnv.exe";
                    process.Start();
                    while (!process.WaitForExit(15000))
                    {
                        process.Kill();
                        process.Start();
                    }
                    Interlocked.Increment(ref convertedFilesCount);
                    UpdateConversionProgress((double)convertedFilesCount / (double)fileCount);
                    return newFileName;
                }
                ,
                new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = token
                }
                );


            var parseCitizenInfo = new ActionBlock<string>(
               (fileName) =>
               {
                   var referendumProcessor = new ReferendumProcessor();
                   citizensOfArmenia.Post(referendumProcessor.ProcessFile(fileName));
               }
               ,
               new ExecutionDataflowBlockOptions()
               {
                   MaxDegreeOfParallelism = Environment.ProcessorCount,
                   CancellationToken = token
               }
               );


            // Link the blocks
            citizensOfArmenia = new BufferBlock<List<Citizen>>();
            getFileUrls.LinkTo(downloadFile, new DataflowLinkOptions { PropagateCompletion = true });
            downloadFile.LinkTo(convertFile, new DataflowLinkOptions { PropagateCompletion = true });
            convertFile.LinkTo(parseCitizenInfo, new DataflowLinkOptions { PropagateCompletion = true });
            parseCitizenInfo.Completion.ContinueWith((tsk) => citizensOfArmenia.Complete());
        }

        public void Initialize(string url)
        {
            getFileUrls.Post(url);
            //completion will propagate to the rest of the pipeline from here
            getFileUrls.Complete();
        }

        private void GetFileUrls(string url, List<string> fileUrls)
        {
            if (url.EndsWith("/"))
            {
                foreach (string link in GetLinks(url))
                {
                    GetFileUrls(link, fileUrls);
                }
            }
            else
            {
                fileCount++;
                fileUrls.Add(url);
            }
        }

        private static List<String> GetLinks(string htmlUrl)
        {
            HtmlWeb hw = new HtmlWeb();
            HtmlDocument doc = hw.Load(htmlUrl);
            List<String> links = new List<String>();
            foreach (HtmlNode node in doc.DocumentNode.SelectNodes("//a[@href]"))
            {
                string link = node.Attributes["href"].Value;
                if (!link.StartsWith("?") && !link.StartsWith("/"))
                {
                    links.Add(htmlUrl + link);
                }

            }
            return links;
        }
    }
}
