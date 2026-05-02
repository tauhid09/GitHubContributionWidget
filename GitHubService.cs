using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

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

        public async Task<ContributionData> GetContributionsAsync(int? year = null)
        {
            string collectionArgs = "";
            if (year.HasValue)
            {
                string fromDate = $"{year.Value}-01-01T00:00:00Z";
                string toDate = $"{year.Value}-12-31T23:59:59Z";
                collectionArgs = $"(from: \"{fromDate}\", to: \"{toDate}\")";
            }

            string query = @"
            {
                user(login: """ + _username + @""") {
                    avatarUrl
                    contributionsCollection" + collectionArgs + @" {
                        contributionCalendar {
                            totalContributions
                            weeks {
                                contributionDays {
                                    contributionCount
                                    date
                                }
                            }
                        }
                        totalCommitContributions
                        totalPullRequestContributions
                        totalIssueContributions
                        totalPullRequestReviewContributions
                        totalRepositoryContributions
                        restrictedContributionsCount
                    }
                    totalYears: contributionsCollection {
                        contributionYears
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
            var userNode = data["data"]["user"];
            var collection = userNode["contributionsCollection"];
            var calendar = collection["contributionCalendar"];

            contributionData.AvatarUrl = userNode["avatarUrl"]?.ToString();

            // totalContributions from the calendar includes public commits, issues, PRs, reviews
            int calendarTotal = (int)calendar["totalContributions"];

            // restrictedContributionsCount = private repo contributions not shown publicly
            int restricted = 0;
            var restrictedToken = collection["restrictedContributionsCount"];
            if (restrictedToken != null)
                restricted = (int)restrictedToken;

            // The true total includes both public and private contributions
            contributionData.TotalContributions = calendarTotal + restricted;

            contributionData.Commits      = (int)collection["totalCommitContributions"];
            contributionData.PullRequests = (int)collection["totalPullRequestContributions"];
            contributionData.Issues       = (int)collection["totalIssueContributions"];
            contributionData.Reviews      = (int)collection["totalPullRequestReviewContributions"];

            // Parse repository contributions (creating new repos)
            var repoContribs = collection["totalRepositoryContributions"];
            if (repoContribs != null)
                contributionData.RepoContributions = (int)repoContribs;

            // Parse actual active years
            var yearsToken = userNode["totalYears"]?["contributionYears"];
            if (yearsToken != null)
            {
                contributionData.Years = yearsToken.Select(y => (int)y).ToList();
            }
            else
            {
                contributionData.Years = new List<int> { DateTime.Now.Year };
            }

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
        public string AvatarUrl { get; set; }
        public int TotalContributions { get; set; }
        public int Commits { get; set; }
        public int PullRequests { get; set; }
        public int Issues { get; set; }
        public int Reviews { get; set; }
        public int RepoContributions { get; set; }
        public List<int> Years { get; set; } = new List<int>();
        public List<ContributionDay> Days { get; set; } = new List<ContributionDay>();
    }

    public class ContributionDay
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }
}