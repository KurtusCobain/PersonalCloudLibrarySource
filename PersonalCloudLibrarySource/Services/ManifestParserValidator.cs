using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace PersonalCloudLibrarySource
{
    public sealed class ManifestParseResult
    {
        public bool Succeeded { get; set; }
        public PersonalCloudLibraryManifest Manifest { get; set; }
        public List<PersonalCloudLibraryItem> ValidItems { get; } = new List<PersonalCloudLibraryItem>();
        public List<string> Issues { get; } = new List<string>();
        public string Error { get; set; } = string.Empty;

        public PersonalCloudLibraryManifest CreateValidatedManifest()
        {
            if (!Succeeded || Manifest == null) return null;
            return new PersonalCloudLibraryManifest
            {
                Version = Manifest.Version,
                GeneratedBy = Manifest.GeneratedBy,
                GeneratedAt = Manifest.GeneratedAt,
                SourceMode = Manifest.SourceMode,
                ItemCount = ValidItems.Count,
                Items = new List<PersonalCloudLibraryItem>(ValidItems)
            };
        }
    }

    public sealed class ManifestParserValidator
    {
        public const int MinimumSupportedVersion = 1;
        public const int MaximumSupportedVersion = 3;

        public ManifestParseResult Parse(string json)
        {
            var result = new ManifestParseResult();
            if (string.IsNullOrWhiteSpace(json))
            {
                result.Error = "Manifest JSON was empty.";
                return result;
            }

            try
            {
                json = json.TrimStart('\uFEFF', '\u00EF', '\u00BB', '\u00BF');
                result.Manifest = new JavaScriptSerializer().Deserialize<PersonalCloudLibraryManifest>(json);
            }
            catch (Exception ex)
            {
                result.Error = "Manifest JSON was invalid: " + ex.Message;
                return result;
            }

            if (result.Manifest == null || result.Manifest.Items == null)
            {
                result.Error = "Manifest was empty or invalid.";
                return result;
            }

            if (result.Manifest.Version < MinimumSupportedVersion || result.Manifest.Version > MaximumSupportedVersion)
            {
                result.Error = "Manifest version " + result.Manifest.Version + " is unsupported.";
                return result;
            }

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < result.Manifest.Items.Count; index++)
            {
                var item = result.Manifest.Items[index];
                if (item == null)
                {
                    result.Issues.Add("Item " + index + " is null.");
                }
                else if (string.IsNullOrWhiteSpace(item.Id))
                {
                    result.Issues.Add("Item " + index + " is missing an id.");
                }
                else if (string.IsNullOrWhiteSpace(item.Title))
                {
                    result.Issues.Add("Item " + item.Id + " is missing a title.");
                }
                else if (!ids.Add(item.Id))
                {
                    result.Issues.Add("Duplicate item id: " + item.Id);
                }
                else
                {
                    result.ValidItems.Add(item);
                }
            }

            result.Succeeded = true;
            return result;
        }
    }
}
