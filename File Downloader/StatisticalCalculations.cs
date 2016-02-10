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
using System.Globalization;

namespace File_Downloader
{
    public class StatisticalCalculations
    {
        private Dictionary<string, int> globalNameFrequency;
        private Dictionary<int, int> globalMonthFrequency;
        private static Mutex mutex;
        private static int totalAge;
        private static int peopleCount;
        private Task extractionCompletion;
        public StatisticalCalculations()
        {
            globalNameFrequency = new Dictionary<string, int>();
            globalMonthFrequency = new Dictionary<int, int>();
            mutex = new Mutex();
            totalAge = 0;
            peopleCount = 0;
        }

        private void ExtractData(ISourceBlock<List<Citizen>> source)
        {
       
            var extractData = new ActionBlock<List<Citizen>>(
             (citizens) =>
             {
                 var today = DateTime.Today;
                 Dictionary<string, int> localNameFrequency = new Dictionary<string, int>();
                 Dictionary<int, int> localMonthFrequency = new Dictionary<int, int>();
                 foreach (Citizen citizen in citizens)
                 {

                     var bday = citizen.Birthday;
                     int age = today.Year - bday.Year;
                     if (bday > today.AddYears(-age))
                     {
                         age--;
                     }
                     Interlocked.Add(ref totalAge, age);
                     Interlocked.Increment(ref peopleCount);


                     string name = citizen.Firstname;

                     if (!localNameFrequency.ContainsKey(name))
                     {
                         localNameFrequency[name] = 0;
                     }
                     localNameFrequency[name]++;

                     int month = citizen.Birthday.Month;

                     if (!localMonthFrequency.ContainsKey(month))
                     {
                         localMonthFrequency[month] = 0;
                     }
                     localMonthFrequency[month]++;

                 }

                 mutex.WaitOne();
                 foreach (string key in localNameFrequency.Keys)
                 {
                     if (!globalNameFrequency.ContainsKey(key))
                     {
                         globalNameFrequency[key] = 0;
                     }
                     globalNameFrequency[key] += localNameFrequency[key];
                 }
                 mutex.ReleaseMutex();


                 mutex.WaitOne();
                 foreach (int key in localMonthFrequency.Keys)
                 {
                     if (!globalMonthFrequency.ContainsKey(key))
                     {
                         globalMonthFrequency[key] = 0;
                     }
                     globalMonthFrequency[key] += localMonthFrequency[key];
                 }
                 mutex.ReleaseMutex();

             }
             , new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }
             );

            source.LinkTo(extractData, new DataflowLinkOptions { PropagateCompletion = true });
            extractionCompletion = extractData.Completion;
        }

        public void CalculateAverage(ISourceBlock<List<Citizen>> source, Action<int, int> PrintAverage)
        {
            if(extractionCompletion == null)
            {
                ExtractData(source);
            }

            extractionCompletion.ContinueWith((tsk) => PrintAverage(totalAge, peopleCount));
        }


        public void FiveMostCommonNames(ISourceBlock<List<Citizen>> source, Action< List<string>, string > PrintResult)
        {
            if (extractionCompletion == null)
            {
                ExtractData(source);
            }
          
            extractionCompletion.ContinueWith(
            (arg) =>
            {
                var sortedDict = from entry in globalNameFrequency orderby entry.Value descending select entry.Key;
                var list = sortedDict.Take(5).ToList();
                PrintResult(list, "common");
            }
            );
        }

        public void FiveMostUncommonNames(ISourceBlock<List<Citizen>> source, Action<List<string>, string> PrintResult)
        {
            if (extractionCompletion == null)
            {
                ExtractData(source);
            }
            extractionCompletion.ContinueWith(
            (arg) =>
            {
                var sortedDict = from entry in globalNameFrequency orderby entry.Value ascending select entry.Key;
                var list = sortedDict.Take(5).ToList();
                PrintResult(list, "uncommon");
            }
            );
        }

        public void TopThreeMonth(ISourceBlock<List<Citizen>> source, Action<List<string>, string> PrintResult)
        {
            if (extractionCompletion == null)
            {
                ExtractData(source);
            }
            extractionCompletion.ContinueWith(
            (arg) =>
            {
                var sortedDict = from entry in globalMonthFrequency orderby entry.Value ascending select entry.Key;
                var list = sortedDict.Take(3).ToList();
                List<string> months = new List<string>();
                foreach(int month in list)
                {
                    months.Add(CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month));
                }
                PrintResult(months, "month");
            }
            );
        }

    }
}