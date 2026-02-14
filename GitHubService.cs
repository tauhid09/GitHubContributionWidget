using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GitHubContributionWidget
{
    public class GitHubService
    {
        private readonly HttpClient _httpClient;
        private readonly string _username;

        public GitHubService(string username, string token)
        {
            _username = username;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("GitHubWidget", "1.0"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<ContributionData> GetContributionsAsync()
        {
            string query = @"
            {
                user(login: """ + _username + @""") {
                    contributionsCollection {
                        contributionCalendar {
                            totalContributions
                            weeks {
                                contributionDays {
                                    contributionCount
                                    date
                                }
                            }
                        }
                    }
                }
            }";

            var request = new
            {
                query = query
            };

            var content = new StringContent(
                Newtonsoft.Json.JsonConvert.SerializeObject(request),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                "https://api.github.com/graphql", content);

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var data = JObject.Parse(jsonResponse);

            return ParseContributionData(data);
        }

        private ContributionData ParseContributionData(JObject data)
        {
            var contributionData = new ContributionData();
            var calendar = data["data"]["user"]["contributionsCollection"]["contributionCalendar"];

            contributionData.TotalContributions = (int)calendar["totalContributions"];

            foreach (var week in calendar["weeks"])
            {
                foreach (var day in week["contributionDays"])
                {
                    contributionData.Days.Add(new ContributionDay
                    {
                        Date = DateTime.Parse(day["date"].ToString()),
                        Count = (int)day["contributionCount"]
                    });
                }
            }

            return contributionData;
        }
    }

    public class ContributionData
    {
        public int TotalContributions { get; set; }
        public List<ContributionDay> Days { get; set; } = new List<ContributionDay>();
    }

    public class ContributionDay
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }
}