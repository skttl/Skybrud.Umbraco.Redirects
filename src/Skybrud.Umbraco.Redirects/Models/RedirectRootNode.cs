using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Umbraco.Cms.Core.Models;

namespace Skybrud.Umbraco.Redirects.Models;

/// <summary>
/// Class representing a root node.
/// </summary>
public class RedirectRootNode {

    /// <summary>
    /// Gets the ID of the root node.
    /// </summary>
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public int Id { get; }

    /// <summary>
    /// Gets the GUID of the root node.
    /// </summary>
    [JsonProperty("key")]
    [JsonPropertyName("key")]
    public Guid Key { get; }

    /// <summary>
    /// Gets the name of the root node.
    /// </summary>
    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string Name { get; }

    /// <summary>
    /// Gets the icon of the root node.
    /// </summary>
    [JsonProperty("icon")]
    [JsonPropertyName("icon")]
    public string Icon { get; }

    /// <summary>
    /// Gets the domains asscoiated with the root node.
    /// </summary>
    [JsonProperty("domains")]
    [JsonPropertyName("domains")]
    public string[] Domains { get; }

    /// <summary>
    /// Initializes a new instance based on the specified <paramref name="content"/> and <paramref name="domains"/>.
    /// </summary>
    /// <param name="content">An instance of <see cref="IContent"/> representing the root node.</param>
    /// <param name="domains">A list of domains associated with the root node.</param>
    public RedirectRootNode(IContent content, IEnumerable<RedirectDomain>? domains) {
        Id = content.Id;
        Key = content.Key;
        Name = content.Name!;
        Icon = content.ContentType.Icon!;
        Domains = domains?.Select(x => x.Name).ToArray() ?? [];
    }

    /// <summary>
    /// Initializes a new instance based on the specified parameters.
    /// </summary>
    /// <param name="id">The ID of the root node.</param>
    /// <param name="key">The GUID key of the root node.</param>
    /// <param name="name">The name of the root node.</param>
    /// <param name="icon">The icon of the root node.</param>
    /// <param name="domains">A list of domains associated with the root node.</param>
    public RedirectRootNode(int id, Guid key, string name, string icon, IEnumerable<RedirectDomain>? domains) {
        Id = id;
        Key = key;
        Name = name;
        Icon = icon;
        Domains = domains?.Select(x => x.Name).ToArray() ?? [];
    }

    /// <summary>
    /// Initiaizes a new instance based on the specified <paramref name="content"/> item.
    /// </summary>
    /// <param name="content">The content item representing the root node.</param>
    /// <param name="domains">The domains asscoiated with the root node.</param>
    /// <returns>An instance of <see cref="RedirectRootNode"/>.</returns>
    [return: NotNullIfNotNull(nameof(content))]
    public static RedirectRootNode? GetFromContent(IContent? content, IEnumerable<RedirectDomain>? domains) {
        return content == null ? null : new RedirectRootNode(content, domains);
    }

}