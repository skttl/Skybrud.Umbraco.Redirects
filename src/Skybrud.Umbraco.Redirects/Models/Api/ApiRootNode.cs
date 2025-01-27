using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Skybrud.Umbraco.Redirects.Helpers;
using Umbraco.Cms.Core.Models;

#pragma warning disable 1591

namespace Skybrud.Umbraco.Redirects.Models.Api;

public class ApiRootNode {

    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonProperty("key")]
    [JsonPropertyName("key")]
    public Guid Key { get; set; }

    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonProperty("icon")]
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonProperty("backOfficeUrl")]
    [JsonPropertyName("backOfficeUrl")]
    public string? BackOfficeUrl { get; set; }

    [JsonProperty("domains")]
    [JsonPropertyName("domains")]
    public IReadOnlyList<string> Domains { get; set; }

    public ApiRootNode(IRedirect redirect, IContent? content, string[]? domains) {
        Id = content?.Id ?? 0;
        Key = content?.Key ?? redirect.RootKey;
        Name = content?.Name;
        Icon = content?.ContentType.Icon;
        Domains = domains ?? [];
        BackOfficeUrl = $"/umbraco/#/content/content/edit/{Id}";
    }

    public ApiRootNode(IRedirect redirect, IContent? content, string[]? domains, string backOfficeBaseUrl) {
        Id = content?.Id ?? 0;
        Key = content?.Key ?? redirect.RootKey;
        Name = content?.Name;
        Icon = content?.ContentType.Icon;
        Domains = domains ?? [];
        BackOfficeUrl = $"{backOfficeBaseUrl}/#/content/content/edit/{Id}";
    }

    public ApiRootNode(IRedirect redirect, IContent? content, string[]? domains, RedirectsBackOfficeHelper backOffice) {
        Id = content?.Id ?? 0;
        Key = content?.Key ?? redirect.RootKey;
        Name = content?.Name;
        Icon = content?.ContentType.Icon;
        Domains = domains ?? [];
        BackOfficeUrl = $"{backOffice.BackOfficeUrl}/#/content/content/edit/{Id}";
    }

    public ApiRootNode(RedirectRootNode rootNode) {
        Id = rootNode.Id;
        Key = rootNode.Key;
        Name = rootNode.Name;
        Icon = rootNode.Icon;
        Domains = rootNode.Domains;
        BackOfficeUrl = $"/umbraco/#/content/content/edit/{Id}";
    }

    public ApiRootNode(RedirectRootNode rootNode, string backOfficeBaseUrl) {
        Id = rootNode.Id;
        Key = rootNode.Key;
        Name = rootNode.Name;
        Icon = rootNode.Icon;
        Domains = rootNode.Domains;
        BackOfficeUrl = $"{backOfficeBaseUrl}/#/content/content/edit/{Id}";
    }

    public ApiRootNode(RedirectRootNode rootNode, RedirectsBackOfficeHelper backOffice) {
        Id = rootNode.Id;
        Key = rootNode.Key;
        Name = rootNode.Name;
        Icon = rootNode.Icon;
        Domains = rootNode.Domains;
        BackOfficeUrl = $"{backOffice.BackOfficeUrl}/#/content/content/edit/{Id}";
    }

}