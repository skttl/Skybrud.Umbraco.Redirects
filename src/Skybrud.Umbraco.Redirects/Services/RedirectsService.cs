﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NPoco;
using Skybrud.Essentials.Common;
using Skybrud.Essentials.Strings.Extensions;
using Skybrud.Essentials.Time;
using Skybrud.Umbraco.Redirects.Exceptions;
using Skybrud.Umbraco.Redirects.Extensions;
using Skybrud.Umbraco.Redirects.Models;
using Skybrud.Umbraco.Redirects.Models.Dtos;
using Skybrud.Umbraco.Redirects.Models.Options;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace Skybrud.Umbraco.Redirects.Services;

/// <summary>
/// Default implementation of the <see cref="IRedirectsService"/> interface.
/// </summary>
public class RedirectsService : IRedirectsService {

    private readonly ILogger<RedirectsService> _logger;
    private readonly IScopeProvider _scopeProvider;
    private readonly IDomainService _domains;
    private readonly IContentService _contentService;
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;

    #region Constructors

    /// <summary>
    /// Initializes a new instance based on the specified <paramref name="dependencies"/>.
    /// </summary>
    /// <param name="dependencies">An instance of <see cref="RedirectsServiceDependencies"/>.</param>
    public RedirectsService(RedirectsServiceDependencies dependencies) {
        _logger = dependencies.Logger;
        _scopeProvider = dependencies.ScopeProvider;
        _domains = dependencies.Domains;
        _contentService = dependencies.ContentService;
        _umbracoContextAccessor = dependencies.UmbracoContextAccessor;
    }

    #endregion

    #region Member methods

    /// <summary>
    /// Gets an array of all domains (<see cref="RedirectDomain"/>) registered in Umbraco.
    /// </summary>
    /// <returns></returns>
    public RedirectDomain[] GetDomains() {
        return _domains.GetAll(false).Select(RedirectDomain.GetFromDomain).ToArray()!;
    }

    /// <summary>
    /// Deletes the specified <paramref name="redirect"/>.
    /// </summary>
    /// <param name="redirect">The redirect to be deleted.</param>
    public void DeleteRedirect(IRedirect redirect) {

        // Some input validation
        if (redirect == null) throw new ArgumentNullException(nameof(redirect));

        // This implementation only supports the "Redirect class"
        if (redirect is not Redirect r) throw new ArgumentException($"Redirect type is not supported: {redirect.GetType()}", nameof(redirect));

        // Create a new scope
        using var scope = _scopeProvider.CreateScope();

        // Remove the redirect from the database
        try {
            scope.Database.Delete(r.Dto);
        } catch (Exception ex) {
            throw new RedirectsException("Unable to delete redirect from database.", ex);
        }

        // Complete the scope
        scope.Complete();

    }

    /// <summary>
    /// Gets the redirect mathing the specified <paramref name="url"/>.
    /// </summary>
    /// <param name="rootNodeKey">The key of the root node. Use <see cref="Guid.Empty"/> for a global redirect.</param>
    /// <param name="url">The URL of the redirect.</param>
    /// <returns>An instance of <see cref="Redirect"/>, or <c>null</c> if not found.</returns>
    public virtual IRedirect? GetRedirectByUrl(Guid rootNodeKey, string url) {
        url.Split('?', out string path, out string? query);
        return GetRedirectByPathAndQuery(rootNodeKey, path, query);
    }

    /// <summary>
    /// Gets the redirect mathing the specified <paramref name="path"/> and <paramref name="query"/>.
    /// </summary>
    /// <param name="rootNodeKey">The key of the root node. Use <see cref="Guid.Empty"/> for a global redirect.</param>
    /// <param name="path">The path of the redirect.</param>
    /// <param name="query">The query string of the redirect.</param>
    /// <returns>An instance of <see cref="Redirect"/>, or <c>null</c> if not found.</returns>
    public virtual IRedirect? GetRedirectByPathAndQuery(Guid rootNodeKey, string path, string? query) {

        // Some input validation
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));

        path = path.Trim().TrimEnd('/');
        query = (query ?? string.Empty).Trim();

        IReadOnlyList<RedirectDto> dtos;

        using (IScope scope = _scopeProvider.CreateScope()) {

            // Generate the base of the SQL for the query
            var sql = scope.SqlContext.Sql()
                .Select<RedirectDto>()
                .From<RedirectDto>();

            // If a specific root node isn't specified, the WHERE clause can be simplified a bit. On the other
            // hand, if a specific root node is specified, the WHERE clause should be looking for both site
            // specific redirects and global redirects. In case there is a match for both a site specific redirect
            // and a global redirect, the ORDER BY clause is used to ensure that we're looking at site specific
            // redirects first, then global redirects second
            if (rootNodeKey == Guid.Empty) {
                sql = sql
                    .Where<RedirectDto>(x => x.RootKey == Guid.Empty && x.Path == path && (x.QueryString == query || x.ForwardQueryString));
            } else {
                sql = sql
                    .Where<RedirectDto>(x => (x.RootKey == rootNodeKey || x.RootKey == Guid.Empty) && x.Path == path && (x.QueryString == query || x.ForwardQueryString))
                    .OrderByDescending<RedirectDto>(x => x.RootKey);
            }

            // Make the call to the database
            dtos = scope.Database.Fetch<RedirectDto>(sql);

            // Finish the scope
            scope.Complete();

        }

        // Return null if we haven't found any redirects at this point
        if (dtos.Count == 0) return null;

        // To support query string forwarding, we should only return a redirect that match either of the two criteria listed below:
        // - query string forwarding isn't enabled and the query string is an exact match
        // - query string forwarding is enabled and the query string is part of the query string of the inbound URI
        string query1 = query.Length == 0 ? string.Empty : $"&{query}&";
        RedirectDto? dto = dtos.FirstOrDefault(x => (!x.ForwardQueryString && query.InvariantEquals(x.QueryString)) || (x.QueryString.Length == 0 || query1.InvariantContains($"&{x.QueryString}&") && x.ForwardQueryString));

        // Wrap the DTO
        return dto == null ? null : new Redirect(dto);

    }

    /// <summary>
    /// Returns the first redirect matching the specified <paramref name="request"/>, or <c>null</c> if the request does not match any redirects.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>An instance of <see cref="IRedirect"/>, or <c>null</c> if no matching redirects were found.</returns>
    public IRedirect? GetRedirectByRequest(HttpRequest request) {

        // Get the URI from the request
        Uri uri = request.GetUriForRedirects();

        // Look for redirects by the URI
        return GetRedirectByUri(uri);

    }

    /// <summary>
    /// Returns the first redirect matching the specified <paramref name="uri"/>, or <c>null</c> if the URI does not match any redirects.
    /// </summary>
    /// <param name="uri">The URI of the request.</param>
    /// <returns>An instance of <see cref="IRedirect"/>, or <c>null</c> if no matching redirects were found.</returns>
    public IRedirect? GetRedirectByUri(Uri uri) {

        // Get the decoded path
        string path = HttpUtility.UrlDecode(uri.AbsolutePath);

        // Get the query string
        string? query = uri.PathAndQuery.Split('?').Skip(1).FirstOrDefault();

        // Get the current Umbraco context
        _umbracoContextAccessor.TryGetUmbracoContext(out IUmbracoContext? umbracoContext);

        // Determine the root node via domain of the request
        Guid rootKey = Guid.Empty;
        if (TryGetDomain(uri, out Domain? domain)) {
            IPublishedContent? root = umbracoContext?.Content?.GetById(domain.ContentId);
            if (root != null) rootKey = root.Key;
        }

        // Look for a matching redirect
        return GetRedirectByPathAndQuery(rootKey, path, query);

    }

    private bool TryGetDomain(Uri uri, [NotNullWhen(true)] out Domain? domain) {
        domain = DomainUtils.FindDomainForUri(_domains, uri);
        return domain != null;
    }

    /// <summary>
    /// Returns the redirect with the  specified numeric <paramref name="redirectId"/>.
    /// </summary>
    /// <param name="redirectId">The numeric ID of the redirect.</param>
    /// <returns>An instance of <see cref="Redirect"/>, or <c>null</c> if not found.</returns>
    public IRedirect? GetRedirectById(int redirectId) {

        // Validate the input
        if (redirectId == 0) throw new ArgumentException("redirectId must have a value", nameof(redirectId));

        RedirectDto dto;

        using (var scope = _scopeProvider.CreateScope()) {

            // Generate the SQL for the query
            var sql = scope.SqlContext.Sql()
                .Select<RedirectDto>()
                .From<RedirectDto>()
                .Where<RedirectDto>(x => x.Id == redirectId);

            // Make the call to the database
            dto = scope.Database.FirstOrDefault<RedirectDto>(sql);
            scope.Complete();

        }

        // Wrap the database row
        return dto == null ? null : new Redirect(dto);

    }

    /// <summary>
    /// Gets the redirect mathing the specified GUID <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The GUID key of the redirect.</param>
    /// <returns>An instance of <see cref="Redirect"/>, or <c>null</c> if not found.</returns>
    public IRedirect? GetRedirectByKey(Guid key) {

        RedirectDto dto;

        using (var scope = _scopeProvider.CreateScope()) {

            // Generate the SQL for the query
            var sql = scope.SqlContext.Sql()
                .Select<RedirectDto>()
                .From<RedirectDto>()
                .Where<RedirectDto>(x => x.Key == key);

            // Make the call to the database
            dto = scope.Database.FirstOrDefault<RedirectDto>(sql);
            scope.Complete();

        }

        // Wrap the database row
        return dto == null ? null : new Redirect(dto);

    }

    /// <inheritdoc />
    public IRedirect AddRedirect(AddRedirectOptions options) {

        if (options == null) throw new ArgumentNullException(nameof(options));

        // Determine the GUID key of the new redirect. Ideally the input key should always be
        // empty, meaning we will generate a new key here, but for imports, we can use the same key
        Guid key = options.Key == Guid.Empty ? Guid.NewGuid() : options.Key;

        // Initialize the destination
        RedirectDestination destination = new() {
            Id = options.Destination.Id,
            Key = options.Destination.Key,
            Name = options.Destination.Name,
            Url = options.Destination.Url,
            // ReSharper disable NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            Query = options.Destination.Query ?? string.Empty,
            Fragment = options.Destination.Fragment ?? string.Empty,
            // ReSharper restore NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            Type = options.Destination.Type,
            Culture = options.Destination.Culture ?? string.Empty
        };

        // Initialize the new redirect and populate the properties
        Redirect item = new Redirect(key) {
            RootKey = options.RootNodeKey,
            Url = options.OriginalUrl!,
            CreateDate = options.CreateDate ?? EssentialsTime.UtcNow,
            UpdateDate = options.CreateDate ?? EssentialsTime.UtcNow,
            Type = options.Type,
            ForwardQueryString = options.ForwardQueryString
        }.SetDestination(destination);

        // Does a matching redirect already exist?
        if (GetRedirectByPathAndQuery(options.RootNodeKey, item.Path, item.QueryString) != null) {
            throw new RedirectAlreadyExistsException(item);
        }

        // Attempt to add the redirect to the database
        using (var scope = _scopeProvider.CreateScope()) {
            try {
                scope.Database.Insert(item.Dto);
            } catch (Exception ex) {
                //_logger.Error<RedirectsService>("Unable to insert redirect into the database", ex);
                throw new RedirectsException("Unable to insert redirect into the database.", ex);
            }
            scope.Complete();
        }

        // Make the call to the database
        return GetRedirectById(item.Id)!;

    }

    /// <summary>
    /// Saves the specified <paramref name="redirect"/>.
    /// </summary>
    /// <param name="redirect">The redirected to be saved.</param>
    /// <returns>The saved <paramref name="redirect"/>.</returns>
    public IRedirect SaveRedirect(IRedirect redirect) {

        // Some input validation
        if (redirect == null) throw new ArgumentNullException(nameof(redirect));

        // This implementation only supports the "Redirect class"
        if (redirect is not Redirect r) throw new ArgumentException($"Redirect type is not supported: {redirect.GetType()}", nameof(redirect));

        // Check whether another redirect matches the new URL and query string
        IRedirect? existing = GetRedirectByPathAndQuery(redirect.RootKey, redirect.Path, redirect.QueryString);
        if (existing != null && existing.Id != redirect.Id) {
            throw new RedirectAlreadyExistsException(redirect);
        }

        // Update the timestamp for when the redirect was modified
        redirect.UpdateDate = DateTime.UtcNow;

        // Create a new scope
        using IScope scope = _scopeProvider.CreateScope();

        // Update the redirect in the database
        try
        {
            scope.Database.Update(r.Dto);
        } catch (Exception ex) {
            _logger.LogError(ex, "Unable to update redirect into the database.");
            throw new RedirectsException("Unable to update redirect into the database.", ex);
        }

        // Complete the scope
        scope.Complete();

        // Return the redirect
        return redirect;

    }

    /// <summary>
    /// Returns a paginated list of redirects matching the specified <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The options the returned redirects should match.</param>
    /// <returns>An instance of <see cref="RedirectsSearchResult"/>.</returns>
    public RedirectsSearchResult GetRedirects(RedirectsSearchOptions options) {

        if (options == null) throw new ArgumentNullException(nameof(options));

        // Create a new scope
        using var scope = _scopeProvider.CreateScope();

        // Generate the SQL for the query
        var sql = scope.SqlContext.Sql().Select<RedirectDto>().From<RedirectDto>();

        // Search by the rootNodeId
        if (options.RootNodeKey != null) sql = sql.Where<RedirectDto>(x => x.RootKey == options.RootNodeKey.Value);

        // Search by the type
        if (options.Type != RedirectTypeFilter.All) {
            string type = options.Type.ToPascalCase();
            sql = sql.Where<RedirectDto>(x => x.DestinationType == type);
        }

        // Search by the text
        if (string.IsNullOrWhiteSpace(options.Text) == false) {

            string[] parts = options.Text.Split('?');

            if (parts.Length == 1) {
                if (int.TryParse(options.Text, out int redirectId)) {
                    sql = sql.Where<RedirectDto>(x => x.Id == redirectId || x.Path.Contains(options.Text) || x.QueryString.Contains(options.Text));
                } else if (Guid.TryParse(options.Text, out Guid redirectKey)) {
                    sql = sql.Where<RedirectDto>(x => x.Key == redirectKey || x.Path.Contains(options.Text) || x.QueryString.Contains(options.Text));
                } else {
                    sql = sql.Where<RedirectDto>(x => x.Path.Contains(options.Text) || x.QueryString.Contains(options.Text));
                }
            } else {
                string url = parts[0];
                string query = parts[1];
                sql = sql.Where<RedirectDto>(x => (
                    x.Path.Contains(options.Text)
                    ||
                    (x.Path.Contains(url) && x.QueryString.Contains(query))
                ));
            }
        }

        // Order the redirects
        sql = sql.OrderByDescending<RedirectDto>(x => x.Updated);

        // Make the call to the database
        List<RedirectDto> all = scope.Database.Fetch<RedirectDto>(sql);

        // Calculate variables used for the pagination
        int limit = options.Limit;
        int pages = (int) Math.Ceiling(all.Count / (double) limit);
        int page = Math.Max(1, Math.Min(options.Page, pages));
        int offset = (page * limit) - limit;

        // Apply pagination and wrap the database rows
        IRedirect[] items = all
            .Skip(offset)
            .Take(limit)
            .Select(x => (IRedirect) Redirect.CreateFromDto(x))
            .ToArray();

        // Wrap the search result
        RedirectsSearchResult result = new(all.Count, limit, offset, page, pages, items);

        // Complete the scope
        scope.Complete();

        // Return the result
        return result;

    }

    /// <summary>
    /// Returns a collection with all redirects.
    /// </summary>
    /// <returns>An instance of <see cref="IEnumerable{Redirect}"/>.</returns>
    public IEnumerable<IRedirect> GetAllRedirects()  {

        // Create a new scope
        using var scope = _scopeProvider.CreateScope();

        // Generate the SQL for the query
        Sql<ISqlContext> sql = scope.SqlContext
            .Sql()
            .Select<RedirectDto>()
            .From<RedirectDto>();

        // Make the call to the database
        IEnumerable<Redirect> redirects = scope.Database
            .Fetch<RedirectDto>(sql)
            .Select(Redirect.CreateFromDto);

        scope.Complete();

        return redirects;

    }

    /// <summary>
    /// Returns an array of all rode nodes configured in Umbraco.
    /// </summary>
    /// <returns>An array of <see cref="RedirectRootNode"/> representing the root nodes.</returns>
    public RedirectRootNode[] GetRootNodes()  {

        // Multiple domains may be configured for a single node, so we need to group the domains before proceeding
        var domainsByRootNodeId = GetDomains().GroupBy(x => x.RootNodeId);

        return (
            from domainGroup in domainsByRootNodeId
            let content =  _contentService.GetById(domainGroup.First().RootNodeId)
            where content is { Trashed: false }
            orderby content.Id
            select RedirectRootNode.GetFromContent(content, domainGroup)
        ).ToArray();

    }

    /// <summary>
    /// Returns the calculated destination URL for the specified <paramref name="redirect"/>.
    /// </summary>
    /// <param name="redirect">The redirect.</param>
    /// <returns>The destination URL.</returns>
    public virtual string GetDestinationUrl(IRedirectBase redirect) {
        return GetDestinationUrl(redirect, null);
    }

    /// <summary>
    /// Returns the calculated destination URL for the specified <paramref name="redirect"/>.
    /// </summary>
    /// <param name="redirect">The redirect.</param>
    /// <param name="uri">The inbound URL.</param>
    /// <returns>The destination URL.</returns>
    public virtual string GetDestinationUrl(IRedirectBase redirect, Uri? uri) {

        // Ideally a redirect should always have a destination URL. If it doesn't, it indicates a malformed redirect
        if (string.IsNullOrWhiteSpace(redirect.Destination.Url)) throw new PropertyNotSetException(nameof(redirect.Destination.Url), "Redirect does not specify a destionation URL.");

        // Get the query string (if any)
        string query = redirect.Destination.Query;

        // Get the fragment (if any)
        string fragment = redirect.Destination.Fragment;

        // Merge the existing query string with the query string of "uri" (e.g. from the inbound request)
        if (uri != null && uri.Query.HasValue() && redirect.ForwardQueryString) query = MergeQueryString(query, uri.Query);

        // For content and media, we need to look up the most recent URL
        IPublishedContent? content = null;
        switch (redirect.Destination.Type) {

            case RedirectDestinationType.Content:
                if (_umbracoContextAccessor.TryGetUmbracoContext(out IUmbracoContext? context))  {
                    content = context.Content?.GetById(redirect.Destination.Key);
                }
                break;

            case RedirectDestinationType.Media:
                if (_umbracoContextAccessor.TryGetUmbracoContext(out context))  {
                    content = context.Media?.GetById(redirect.Destination.Key);
                }
                break;

        }

        // For legacy reasons, the package saves empty values as empty strings instead of null values, but for
        // variants, specifying an emtpy string or null when calling the "Url" method gives you different results,
        // so we need to account for that
        string? culture = redirect.Destination.Culture.NullIfWhiteSpace();

        // If the destination is a published content or media item, we try to get the current URL of that item
        // instead of the saved URL, as the item's URL may have changed. Content that vary by culture are a bit
        // tricky, as the extension "Url()" extension method will return "#" if supplied an unsupported culture
        string? contentUrl = null;
        if (content is not null) {
            if (string.IsNullOrWhiteSpace(culture)) {
                contentUrl = content.Url();
            } else {
                contentUrl = content.Url(culture);
                if (contentUrl == "#" || string.IsNullOrWhiteSpace(contentUrl)) contentUrl = content.Url();
            }
        }

        // Better be sure
        if (contentUrl == "#") contentUrl = null;

        // Put the destination URL back together
        return RedirectsUtils.ConcatUrl(contentUrl ?? redirect.Destination.Url, query, fragment);

    }

    /// <summary>
    /// Returns the combined query string value for <paramref name="query1"/> and <paramref name="query2"/>.
    /// </summary>
    /// <param name="query1">The first query string.</param>
    /// <param name="query2">The second query string.</param>
    /// <returns>The combined query string.</returns>
    protected virtual string MergeQueryString(string query1, string query2) {
        if (!query1.HasValue()) return query2.TrimStart('?');
        return query1 + "&" + query2.TrimStart('?');
    }

    /// <summary>
    /// Returns an array of redirects where the destination matches the specified <paramref name="nodeType"/> and <paramref name="nodeId"/>.
    /// </summary>
    /// <param name="nodeType">The type of the destination node.</param>
    /// <param name="nodeId">The numeric ID of the destination node.</param>
    /// <returns>An array of <see cref="IRedirect"/>.</returns>
    public virtual IRedirect[] GetRedirectsByNodeId(RedirectDestinationType nodeType, int nodeId) {
        if (nodeType == RedirectDestinationType.Url) throw new RedirectsException($"Unsupported node type: {nodeType}");
        return GetRedirectsByNodeId(nodeType.ToString(), nodeId);
    }

    /// <summary>
    /// Returns an array of redirects where the destination matches the specified <paramref name="nodeType"/> and <paramref name="nodeKey"/>.
    /// </summary>
    /// <param name="nodeType">The type of the destination node.</param>
    /// <param name="nodeKey">The key (GUID) of the destination node.</param>
    /// <returns>An array of <see cref="IRedirect"/>.</returns>
    public virtual IRedirect[] GetRedirectsByNodeKey(RedirectDestinationType nodeType, Guid nodeKey) {
        if (nodeType == RedirectDestinationType.Url) throw new RedirectsException($"Unsupported node type: {nodeType}");
        return GetRedirectsByNodeKey(nodeType.ToString(), nodeKey);
    }

    /// <summary>
    /// Returns an array of redirects where the destination matches the specified <paramref name="nodeType"/> and <paramref name="nodeId"/>.
    /// </summary>
    /// <param name="nodeType">The type of the destination node.</param>
    /// <param name="nodeId">The numeric ID of the destination node.</param>
    /// <returns>An array of <see cref="IRedirect"/>.</returns>
    protected virtual IRedirect[] GetRedirectsByNodeId(string nodeType, int nodeId) {

        // Create a new scope
        using var scope = _scopeProvider.CreateScope();

        // Generate the SQL for the query
        Sql<ISqlContext> sql = scope.SqlContext
            .Sql()
            .Select<RedirectDto>()
            .From<RedirectDto>()
            .Where<RedirectDto>(x => x.DestinationType == nodeType && x.DestinationId == nodeId);

        // Make the call to the database
        var rows = scope.Database
            .Fetch<RedirectDto>(sql)
            .Select(x => (IRedirect) Redirect.CreateFromDto(x))
            .ToArray();

        // Complete the scope
        scope.Complete();

        return rows;

    }

    /// <summary>
    /// Returns an array of redirects where the destination matches the specified <paramref name="nodeType"/> and <paramref name="nodeKey"/>.
    /// </summary>
    /// <param name="nodeType">The type of the destination node.</param>
    /// <param name="nodeKey">The key (GUID) of the destination node.</param>
    /// <returns>An array of <see cref="IRedirect"/>.</returns>
    protected virtual IRedirect[] GetRedirectsByNodeKey(string nodeType, Guid nodeKey) {

        // Create a new scope
        using var scope = _scopeProvider.CreateScope();

        // Generate the SQL for the query
        Sql<ISqlContext> sql = scope.SqlContext
            .Sql()
            .Select<RedirectDto>().From<RedirectDto>()
            .Where<RedirectDto>(x => x.DestinationType == nodeType && x.DestinationKey == nodeKey);

        // Make the call to the database
        var rows = scope.Database
            .Fetch<RedirectDto>(sql)
            .Select(x => (IRedirect) Redirect.CreateFromDto(x))
            .ToArray();

        // Complete the scope
        scope.Complete();

        return rows;

    }

    #endregion

}