using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;

namespace launcher_m.Core
{
    public class ModrinthService
    {
        private static readonly HttpClient client = new HttpClient();

        public ModrinthService()
        {
            if (!client.DefaultRequestHeaders.Contains("User-Agent"))
                client.DefaultRequestHeaders.Add("User-Agent", "launcher_m/1.0.0 (educational project)");
        }

        public async Task<List<ModrinthProject>> SearchAddons(string query, string type, string version)
        {
            try
            {
                string facets = $"[[\"project_type:{type}\"]]";


                if (System.Text.RegularExpressions.Regex.IsMatch(version, @"^1\.\d+"))
                {
                    facets = $"[[\"project_type:{type}\"],[\"versions:{version}\"]]";
                }

                string encodedFacets = Uri.EscapeDataString(facets);

                string url = $"https://api.modrinth.com/v2/search?query={Uri.EscapeDataString(query)}&facets={encodedFacets}&index=downloads&limit=50";

                System.Diagnostics.Debug.WriteLine("DEBUG URL: " + url);

                var response = await client.GetFromJsonAsync<ModrinthSearchResult>(url);
                return response?.Hits ?? new List<ModrinthProject>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("API ERROR: " + ex.Message);
                return new List<ModrinthProject>();
            }
        }

        public async Task<(string Url, string FileName)?> GetDownloadInfo(string projectId, string mcVersion)
        {
            try
            {
                string url = $"https://api.modrinth.com/v2/project/{projectId}/version";
                var versions = await client.GetFromJsonAsync<List<ModrinthVersion>>(url);

                if (versions == null || versions.Count == 0) return null;

                var targetVersion = versions.Find(v => v?.GameVersions?.Contains(mcVersion) == true) ?? versions[0];

                if (targetVersion?.Files?.Count > 0)
                {
                    return (targetVersion.Files[0].Url, targetVersion.Files[0].FileName);
                }
            }
            catch { }
            return null;
        }
    }
    public class ModrinthSearchResult
    {
        [JsonPropertyName("hits")]
        public List<ModrinthProject> Hits { get; set; } = new();
    }

    public class ModrinthProject
    {
        [JsonPropertyName("project_id")]
        public string? ProjectId { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("icon_url")]
        public string? IconUrl { get; set; }
    }

    public class ModrinthVersion
    {
        [JsonPropertyName("game_versions")] public List<string>? GameVersions { get; set; }
        [JsonPropertyName("files")] public List<ModrinthFile>? Files { get; set; }
    }

    public class ModrinthFile
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("filename")]
        public string FileName { get; set; } = string.Empty;
    }
}