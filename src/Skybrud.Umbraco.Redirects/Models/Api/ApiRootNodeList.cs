using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Skybrud.Umbraco.Redirects.Models.Api;

/// <summary>
/// Class representing a list of <see cref="ApiRootNode"/>.
/// </summary>
public class ApiRootNodeList {

    /// <summary>
    /// Gets the total amount of root nodes.
    /// </summary>
    [JsonPropertyName("total")]
    public int Total { get; }

    /// <summary>
    /// Gets the individual root node items.
    /// </summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<ApiRootNode> Items { get; }

    /// <summary>
    /// Initializes a new instance based on <paramref name="total"/> and <paramref name="items"/>.
    /// </summary>
    /// <param name="total">The total amount of root nodes.</param>
    /// <param name="items">The individual root node items.</param>
    public ApiRootNodeList(int total, IReadOnlyList<ApiRootNode> items) {
        Total = total;
        Items = items;
    }

    /// <summary>
    /// Initializes a new instance based on <paramref name="items"/>.
    /// </summary>
    /// <param name="items">The individual root node items.</param>
    public ApiRootNodeList(IReadOnlyList<ApiRootNode> items) {
        Total = items.Count;
        Items = items;
    }

}