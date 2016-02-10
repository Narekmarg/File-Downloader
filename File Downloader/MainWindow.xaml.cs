using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net.Http;
using System.IO;
using HtmlAgilityPack;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using System.Threading;
using System.Windows.Threading;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.Win32;
using System.Windows.Forms;

namespace File_Downloader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static string _address = "http://www.police.am/Hanraqve/";
        static CancellationTokenSource cts = new CancellationTokenSource();
        static Pipeline pipeline;
        static string testFolder;
        public MainWindow()
        {
            InitializeComponent();
            pipeline = new Pipeline();
        }

        private async void start_btn_Click(object sender, RoutedEventArgs e)
        {
            // Create the Pipeline

            pipeline.BuildPipeline(cts.Token);
            pipeline.UpdateProgress += UpdateProgress;
            pipeline.UpdateConversionProgress += UpdateConversionProgress;
            pipeline.Initialize(_address);
            
        }

        private void UpdateProgress(double progress)
        {
            Action action = delegate ()
            {
                progress_bar.Value = progress;
            };

            Dispatcher.Invoke(DispatcherPriority.Normal, action);
        }

        private void download_folder_btn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            var result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                pipeline.FolderPath = dialog.SelectedPath;
            }


        }

        private void conversion_folder_btn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            var result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                pipeline.ConvertedFilesFolder = dialog.SelectedPath;
            }

        }


        private void folder_cancel_btn_Click(object sender, RoutedEventArgs e)
        {
            cts.Cancel();
        }

        private void PrintAverage(int sum, int cnt)
        {
            Action action = delegate ()
            {
                average_age.Text = ((double)sum / (double)cnt).ToString();
            };

            Dispatcher.Invoke(DispatcherPriority.Normal, action);
        }

        private void UpdateConversionProgress(double progress)
        {
            Action action = delegate ()
            {
                conversion_progress_bar.Value = progress;
            };

            Dispatcher.Invoke(DispatcherPriority.Normal, action);
        }

        private void PrintResult(List<string> names, string type)
        {
            string result = "";
            for (int i = 0; i < names.Count - 1; i++)
            {
                result += names[i];
                result += ", ";
            }
            result += names.Last();

            Action action = delegate ()
            {
                if (type == "common")
                {
                    most_common.Text = result;
                }
                else if(type == "uncommon")
                {
                    most_uncommon.Text = result;
                }
                else if(type == "month")
                {
                    top_three.Text = result;
                }
            };

            Dispatcher.Invoke(DispatcherPriority.Normal, action);
        }

        private void test(string folder)
        {
            string[] files = Directory.GetFiles(folder, "*.xlsx", SearchOption.TopDirectoryOnly);
            var parseCitizenInfo = new TransformBlock<string, List<Citizen>>(
             (fileName) =>
             {
                 var referendumProcessor = new ReferendumProcessor();
                 var cit = referendumProcessor.ProcessFile(fileName);
                 return cit;
             }
             ,
             new ExecutionDataflowBlockOptions()
             {
                 MaxDegreeOfParallelism = Environment.ProcessorCount  
             }
             );
           
            foreach (string file in files)
            {
                parseCitizenInfo.Post(file);
            }

            parseCitizenInfo.Complete();

            StatisticalCalculations calc = new StatisticalCalculations();
            calc.FiveMostUncommonNames(parseCitizenInfo, PrintResult);
            calc.FiveMostCommonNames(parseCitizenInfo, PrintResult);
            calc.TopThreeMonth(parseCitizenInfo, PrintResult);
            calc.CalculateAverage(parseCitizenInfo, PrintAverage);
        }

        private void test_folder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            var result = dialog.ShowDialog();
            if(result == System.Windows.Forms.DialogResult.OK)
            {
                testFolder = dialog.SelectedPath;   
            } 
            
        }

        private void start_calc_Click(object sender, RoutedEventArgs e)
        {
            StatisticalCalculations calc = new StatisticalCalculations();
            calc.FiveMostUncommonNames(pipeline.citizensOfArmenia, PrintResult);
            calc.FiveMostCommonNames(pipeline.citizensOfArmenia, PrintResult);
            calc.TopThreeMonth(pipeline.citizensOfArmenia, PrintResult);
            calc.CalculateAverage(pipeline.citizensOfArmenia, PrintAverage);
        }

        private void start_test_calc_Click(object sender, RoutedEventArgs e)
        {
            test(testFolder);
        }
    }
}
