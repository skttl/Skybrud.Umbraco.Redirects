﻿using NewtonsoftJsonConverter = Newtonsoft.Json.JsonConverterAttribute;
using MicrosoftJsonConverter = System.Text.Json.Serialization.JsonConverterAttribute;

namespace Skybrud.Umbraco.Redirects.Models;

/// <summary>
/// Enum class indicating the type of a redirect - e.g. <see cref="Permanent"/> or <see cref="Temporary"/>.
/// </summary>
[NewtonsoftJsonConverter(typeof(Essentials.Json.Newtonsoft.Converters.Enums.EnumCamelCaseConverter))]
[MicrosoftJsonConverter(typeof(Text.Json.Enums.EnumCamelCaseConverter))]
public enum RedirectType {

    /// <summary>
    /// Indicates that a redirect is permanent.
    /// </summary>
    Permanent,

    /// <summary>
    /// Indicates that a redirect is temporary.
    /// </summary>
    Temporary

}