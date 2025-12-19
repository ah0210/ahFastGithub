using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace FastGithub.UI
{
    /// <summary>
    /// FlowChart.xaml 的交互逻辑
    /// </summary>
    public partial class FlowChart : UserControl
    {
        private readonly LineSeries readSeries = new LineSeries
        {
            Title = "上行速率",
            PointGeometry = null,
            LineSmoothness = 1D,
            Values = new ChartValues<RateTick>()
        };

        private readonly LineSeries writeSeries = new LineSeries()
        {
            Title = "下行速率",
            PointGeometry = null,
            LineSmoothness = 1D,
            Values = new ChartValues<RateTick>()
        };

        private static DateTime GetDateTime(double timestamp) => new DateTime(1970, 1, 1).Add(TimeSpan.FromMilliseconds(timestamp)).ToLocalTime();

        private static double GetTimestamp(DateTime dateTime) => dateTime.ToUniversalTime().Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;


        public SeriesCollection Series { get; } = new SeriesCollection(Mappers.Xy<RateTick>().X(item => item.Timestamp).Y(item => item.Rate));

        public Func<double, string> XFormatter { get; } = timestamp => GetDateTime(timestamp).ToString("HH:mm:ss");

        public Func<double, string> YFormatter { get; } = value => $"{FlowStatistics.ToNetworkSizeString((long)value)}/s";

        public FlowChart()
        {
            InitializeComponent();

            this.Series.Add(this.readSeries);
            this.Series.Add(this.writeSeries);

            this.DataContext = this;
            this.InitFlowChartAsync();
        }

        private async void InitFlowChartAsync()
        {
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(1d) // 设置超时时间，避免长时间阻塞
            };
            
            // 尝试不同的端口，因为GlobalListener会尝试获取可用端口
            var ports = new[] { 80, 3880, 38457 }; // 默认端口和可能的备用端口
            
            while (this.Dispatcher.HasShutdownStarted == false)
            {
                try
                {
                    await this.FlushFlowStatisticsAsync(httpClient, ports);
                }
                catch (Exception)
                {
                    // 忽略异常，继续尝试
                }
                finally
                {
                    await Task.Delay(TimeSpan.FromSeconds(1d));
                }
            }
        }

        private async Task FlushFlowStatisticsAsync(HttpClient httpClient, int[] ports)
        {
            // 尝试所有可能的端口
            foreach (var port in ports)
            {
                try
                {
                    var url = $"http://localhost:{port}/flowStatistics";
                    var response = await httpClient.GetAsync(url);
                    var json = await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync();
                    var flowStatistics = JsonConvert.DeserializeObject<FlowStatistics>(json);
                    if (flowStatistics == null)
                    {
                        return;
                    }

                    this.textBlockRead.Text = FlowStatistics.ToNetworkSizeString(flowStatistics.TotalRead);
                    this.textBlockWrite.Text = FlowStatistics.ToNetworkSizeString(flowStatistics.TotalWrite);

                    var timestamp = GetTimestamp(DateTime.Now);
                    this.readSeries.Values.Add(new RateTick(flowStatistics.ReadRate, timestamp));
                    this.writeSeries.Values.Add(new RateTick(flowStatistics.WriteRate, timestamp));

                    if (this.readSeries.Values.Count > 60)
                    {
                        this.readSeries.Values.RemoveAt(0);
                        this.writeSeries.Values.RemoveAt(0);
                    }
                    
                    break; // 成功获取数据，退出循环
                }
                catch (Exception)
                {
                    // 尝试下一个端口
                    continue;
                }
            }
        }

        private class RateTick
        {
            public double Rate { get; }

            public double Timestamp { get; }

            public RateTick(double rate, double timestamp)
            {
                this.Rate = rate;
                this.Timestamp = timestamp;
            }
        }

    }
}
