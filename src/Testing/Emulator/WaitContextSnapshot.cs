// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Numerics;
using System.Text.Json;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

internal sealed class WaitContextSnapshot
{
    private readonly GatewayContext _template;
    private readonly SendRequestHandler _sendRequest;
    private readonly CacheLookupValueHandler _cacheLookup;

    internal string SectionName { get; }

    internal WaitContextSnapshot(GatewayContext context)
    {
        SectionName = context.CurrentSectionName
            ?? throw new InvalidOperationException("Wait must execute within a section proxy.");
        if (SectionName is not (nameof(IInboundContext) or nameof(IBackendContext)
            or nameof(IOutboundContext) or nameof(IOnErrorContext)))
        {
            throw new NotSupportedException($"Wait cannot execute in runtime section '{SectionName}'.");
        }
        var handlers = context.CurrentSectionHandlers
            ?? throw new InvalidOperationException("Wait requires the calling section's policy handlers.");
        if (handlers.GetValueOrDefault(nameof(IInboundContext.SendRequest)) is not SendRequestHandler sendRequest
            || handlers.GetValueOrDefault(nameof(IInboundContext.CacheLookupValue)) is not CacheLookupValueHandler cacheLookup)
        {
            throw new NotSupportedException("Wait requires the standard SendRequest and CacheLookupValue handlers.");
        }

        _sendRequest = CopySendRequest(sendRequest);
        _cacheLookup = CopyCacheLookup(cacheLookup);
        _template = new SnapshotCopier().CopyContext(context, CancellationToken.None);
        foreach (var cachingType in new[] { "internal", "external", "prefer-external" })
        {
            if (CachePolicyServices.Resolve(context, cachingType) is { } cache)
            {
                _template.Services.Register<ICache>(cachingType, cache);
            }
        }
    }

    internal GatewayContext CreateBranch(CancellationToken cancellationToken) =>
        new SnapshotCopier().CopyContext(_template, cancellationToken);

    internal Dictionary<string, IPolicyHandler> CreateHandlers() => new(StringComparer.Ordinal)
    {
        [nameof(IInboundContext.SendRequest)] = CopySendRequest(_sendRequest),
        [nameof(IInboundContext.CacheLookupValue)] = CopyCacheLookup(_cacheLookup)
    };

    internal WaitVariableChange[] CaptureChanges(GatewayContext branch)
    {
        ArgumentNullException.ThrowIfNull(branch.Variables);
        var copier = new SnapshotCopier();
        var changes = new List<WaitVariableChange>();
        foreach (var name in _template.Variables.Keys.Union(branch.Variables.Keys, _template.Variables.Comparer))
        {
            var existed = _template.Variables.TryGetValue(name, out var original);
            var exists = branch.Variables.TryGetValue(name, out var current);
            if (existed == exists && Equivalent(original, current))
            {
                continue;
            }
            changes.Add(new WaitVariableChange(name, exists, exists ? copier.CopyValue(current) : null));
        }
        return changes.ToArray();
    }

    internal static object? CopyVariable(object? value) => new SnapshotCopier().CopyValue(value);

    private static SendRequestHandler CopySendRequest(SendRequestHandler source)
    {
        var handler = new SendRequestHandler();
        handler.CallbackSetup.AddRange(source.CallbackSetup);
        return handler;
    }

    private static CacheLookupValueHandler CopyCacheLookup(CacheLookupValueHandler source)
    {
        var handler = new CacheLookupValueHandler();
        handler.CallbackSetup.AddRange(source.CallbackSetup);
        handler.ValueSetup.AddRange(source.ValueSetup);
        return handler;
    }

    private static bool Equivalent(object? left, object? right) => Equivalent(left, right, []);

    private static bool Equivalent(object? left, object? right, HashSet<(object, object)> compared)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }
        if (left is null || right is null || left.GetType() != right.GetType())
        {
            return false;
        }
        if (!compared.Add((left, right)))
        {
            return true;
        }
        return (left, right) switch
        {
            (MockResponse first, MockResponse second) =>
                first.StatusCode == second.StatusCode && first.StatusReason == second.StatusReason
                && first.Body.Content == second.Body.Content && first.Body.Consumed == second.Body.Consumed
                && first.Headers.Count == second.Headers.Count
                && first.Headers.All(header => second.Headers.TryGetValue(header.Key, out var values)
                    && header.Value.SequenceEqual(values)),
            (Array first, Array second) =>
                first.Rank == 1 && second.Rank == 1 && first.Length == second.Length
                && Enumerable.Range(0, first.Length).All(index =>
                    Equivalent(first.GetValue(index), second.GetValue(index), compared)),
            (JToken first, JToken second) => JToken.DeepEquals(first, second),
            (XNode first, XNode second) => XNode.DeepEquals(first, second),
            (JsonElement first, JsonElement second) => first.GetRawText() == second.GetRawText(),
            _ => left.Equals(right)
        };
    }

    private sealed class SnapshotCopier
    {
        private readonly Dictionary<object, object> _copies = new(ReferenceEqualityComparer.Instance);

        internal GatewayContext CopyContext(GatewayContext source, CancellationToken cancellationToken)
        {
            if (source.GetType() != typeof(GatewayContext))
            {
                throw Unsupported(source);
            }
            ArgumentNullException.ThrowIfNull(source.Request);
            ArgumentNullException.ThrowIfNull(source.Deployment);
            if (source.Request.Certificate is not null || source.Deployment.Certificates.Count != 0
                || source.CertificateStore.ById.Count != 0 || source.CertificateStore.ByThumbprint.Count != 0)
            {
                throw new NotSupportedException(
                    "Wait cannot safely isolate caller-owned gateway certificates and their transport lifetimes. " +
                    "Use per-request certificate bodies or an explicit WaitBranches callback.");
            }
            var target = new GatewayContext
            {
                RequestId = source.RequestId,
                Timestamp = source.Timestamp,
                Elapsed = source.Elapsed,
                Tracing = source.Tracing,
                Api = CopyContextApi(source.Api),
                Request = CopyRequest(source.Request),
                Response = CopyResponse(source.Response),
                Subscription = CopySubscription(source.Subscription),
                User = CopyUser(source.User),
                Deployment = CopyDeployment(source.Deployment),
                LastError = CopyLastError(source.LastError),
                Operation = CopyOperation(source.Operation),
                Product = CopyProduct(source.Product),
                Trace = source.Trace,
                ResponseTerminated = source.ResponseTerminated,
                BackendUrl = source.BackendUrl,
                ManagedIdentityTokenProvider = source.ManagedIdentityTokenProvider,
                CurrentSectionName = source.CurrentSectionName,
                Variables = CopyVariables(source.Variables)
            };
            target.Services.Register(new HttpTransportState
            {
                CancellationToken = cancellationToken,
                Proxy = source.Services.Resolve<HttpTransportState>()?.Proxy
            });
            source.Services.CopyTo(target.Services);
            source.CopyNamedValuesTo(target);
            foreach (var fragment in source.FragmentRegistry)
            {
                target.FragmentRegistry.Add(fragment.Key, fragment.Value);
            }
            target.ActiveFragments.UnionWith(source.ActiveFragments);
            return target;
        }

        private Dictionary<string, object> CopyVariables(Dictionary<string, object> source)
        {
            ArgumentNullException.ThrowIfNull(source);
            ValidateComparer(source.Comparer);
            var target = source.GetType() == typeof(ApimVariablesDictionary)
                ? new ApimVariablesDictionary()
                : source.GetType() == typeof(Dictionary<string, object>)
                    ? new Dictionary<string, object>(source.Comparer)
                    : throw Unsupported(source);
            foreach (var variable in source)
            {
                target.Add(variable.Key, CopyValue(variable.Value)!);
            }
            return target;
        }

        internal object? CopyValue(object? value)
        {
            if (value is null or string or bool or char or byte or sbyte or short or ushort or int or uint
                or long or ulong or float or double or decimal or DateTime or DateTimeOffset or TimeSpan
                or Guid or BigInteger || value.GetType().IsEnum || value.GetType() == typeof(Uri))
            {
                return value;
            }
            switch (value)
            {
                case JToken token when token.Annotations<object>().Any():
                    throw new NotSupportedException("Wait cannot isolate mutable JSON annotations.");
                case MockResponse response:
                    return CopyResponse(response);
                case Array array when array.Rank == 1 && array.GetLowerBound(0) == 0:
                    if (_copies.TryGetValue(array, out var copied))
                    {
                        return copied;
                    }
                    var clone = (Array)array.Clone();
                    _copies.Add(array, clone);
                    for (var index = 0; index < array.Length; index++)
                    {
                        clone.SetValue(CopyValue(array.GetValue(index)), index);
                    }
                    return clone;
                case JObject json:
                    return Copy(json, source => new JObject(source.Properties().Select(property =>
                        new JProperty(property.Name, CopyJson(property.Value)))));
                case JArray json:
                    return Copy(json, source => new JArray(source.Select(CopyJson)));
                case JValue json:
                    return Copy(json, source => new JValue(CopyValue(source.Value)));
                case XElement element:
                    ValidateXml(element);
                    return Copy(element, source => new XElement(source));
                case XDocument document:
                    ValidateXml(document);
                    return Copy(document, source => new XDocument(source));
                case JsonElement element:
                    return element.Clone();
                default:
                    throw Unsupported(value);
            }
        }

        private JToken CopyJson(JToken token)
        {
            return CopyValue(token) as JToken
                ?? throw new NotSupportedException($"Wait cannot isolate JSON token '{token.Type}'.");
        }

        private static void ValidateXml(XContainer container)
        {
            var elements = container is XElement element ? element.DescendantsAndSelf() : container.Descendants();
            if (container.DescendantNodes().Prepend(container).Cast<XObject>()
                .Concat(elements.SelectMany(element => element.Attributes()))
                .Any(value => value.Annotations<object>().Any()))
            {
                throw new NotSupportedException("Wait cannot isolate mutable XML annotations.");
            }
        }

        private T Copy<T>(T source, Func<T, T> create) where T : class
        {
            ArgumentNullException.ThrowIfNull(source);
            if (source.GetType() != typeof(T))
            {
                throw Unsupported(source);
            }
            if (_copies.TryGetValue(source, out var copied))
            {
                return (T)copied;
            }
            var result = create(source);
            _copies.Add(source, result);
            return result;
        }

        private MockResponse CopyResponse(MockResponse source) => Copy(source, response => new MockResponse
        {
            StatusCode = response.StatusCode,
            StatusReason = response.StatusReason,
            Body = Copy(response.Body, body => body.CopyForWait()),
            Headers = CopyHeaders(response.Headers)
        });

        private MockRequest CopyRequest(MockRequest source) => Copy(source, request => new MockRequest
        {
            Body = Copy(request.Body, body => body.CopyForWait()),
            Headers = CopyHeaders(request.Headers),
            IpAddress = request.IpAddress,
            MatchedParameters = CopyParameters(request.MatchedParameters),
            Method = request.Method,
            OriginalUrl = CopyUrl(request.OriginalUrl),
            Url = CopyUrl(request.Url),
            PrivateEndpointConnection = request.PrivateEndpointConnection is { } endpoint
                ? Copy(endpoint, connection => new MockPrivateEndpointConnection
                {
                    Name = connection.Name,
                    GroupId = connection.GroupId,
                    MemberName = connection.MemberName
                })
                : null,
            AzureVnetInfo = request.AzureVnetInfo is { } vnet
                ? Copy(vnet, info => new MockAzureVnetInfo
                {
                    VnetTrafficTag = info.VnetTrafficTag,
                    SubnetId = info.SubnetId,
                    PrivateLinkId = info.PrivateLinkId,
                    SnatVip = info.SnatVip
                })
                : null
        });

        private Dictionary<string, string[]> CopyHeaders(Dictionary<string, string[]> source)
        {
            ValidateComparer(source.Comparer);
            return Copy(source, headers => headers.ToDictionary(header => header.Key,
                header => (string[])CopyValue(header.Value)!, headers.Comparer));
        }

        private Dictionary<string, string> CopyParameters(Dictionary<string, string> source)
        {
            ValidateComparer(source.Comparer);
            return Copy(source, parameters => new Dictionary<string, string>(parameters, parameters.Comparer));
        }

        private MockUrl CopyUrl(MockUrl source) => Copy(source, url => new MockUrl
        {
            Scheme = url.Scheme,
            Host = url.Host,
            Port = url.Port,
            Path = url.Path,
            Query = CopyHeaders(url.Query)
        });

        private MockContextApi CopyContextApi(MockContextApi source) => Copy(source, api => new MockContextApi
        {
            Id = api.Id,
            Name = api.Name,
            Path = api.Path,
            Protocols = api.Protocols.ToArray(),
            ServiceUrl = CopyUrl(api.ServiceUrl),
            SubscriptionKeyParameterNames = CopySubscriptionKeyNames(api.SubscriptionKeyParameterNames),
            IsCurrentRevision = api.IsCurrentRevision,
            Revision = api.Revision,
            Version = api.Version
        });

        private MockApi CopyApi(IApi source) => source switch
        {
            MockContextApi api => CopyContextApi(api),
            MockApi api => Copy(api, value => new MockApi
            {
                Id = value.Id,
                Name = value.Name,
                Path = value.Path,
                Protocols = value.Protocols.ToArray(),
                ServiceUrl = CopyUrl(value.ServiceUrl),
                SubscriptionKeyParameterNames = CopySubscriptionKeyNames(value.SubscriptionKeyParameterNames)
            }),
            _ => throw Unsupported(source)
        };

        private MockSubscriptionKeyParameterNames CopySubscriptionKeyNames(MockSubscriptionKeyParameterNames source) =>
            Copy(source, names => new MockSubscriptionKeyParameterNames { Header = names.Header, Query = names.Query });

        private MockSubscription CopySubscription(MockSubscription source) => Copy(source, subscription => new MockSubscription
        {
            CreatedDate = subscription.CreatedDate,
            EndDate = subscription.EndDate,
            Id = subscription.Id,
            Key = subscription.Key,
            Name = subscription.Name,
            PrimaryKey = subscription.PrimaryKey,
            SecondaryKey = subscription.SecondaryKey,
            StartDate = subscription.StartDate
        });

        private MockGroup CopyGroup(IGroup source) => source is MockGroup group
            ? Copy(group, value => new MockGroup { Id = value.Id, Name = value.Name })
            : throw Unsupported(source);

        private MockUser CopyUser(MockUser source) => Copy(source, user => new MockUser
        {
            Email = user.Email,
            FirstName = user.FirstName,
            Id = user.Id,
            LastName = user.LastName,
            Note = user.Note,
            RegistrationDate = user.RegistrationDate,
            MockGroups = user.MockGroups.Select(CopyGroup).ToList(),
            MockUserIdentities = user.MockUserIdentities.Select(identity => Copy(identity, value =>
                new MockUserIdentity { Id = value.Id, Provider = value.Provider })).ToList()
        });

        private MockDeployment CopyDeployment(MockDeployment source) => Copy(source, deployment => new MockDeployment
        {
            GatewayId = deployment.GatewayId,
            Region = deployment.Region,
            ServiceId = deployment.ServiceId,
            ServiceName = deployment.ServiceName,
            Certificates = new(deployment.Certificates.Comparer)
        });

        private MockLastError CopyLastError(MockLastError source) => Copy(source, error => new MockLastError
        {
            Source = error.Source,
            Reason = error.Reason,
            Message = error.Message,
            Scope = error.Scope,
            Section = error.Section,
            Path = error.Path,
            PolicyId = error.PolicyId,
            HttpErrorCode = error.HttpErrorCode
        });

        private MockOperation CopyOperation(MockOperation source) => Copy(source, operation => new MockOperation
        {
            Id = operation.Id,
            Method = operation.Method,
            Name = operation.Name,
            UrlTemplate = operation.UrlTemplate
        });

        private MockProduct CopyProduct(MockProduct source) => Copy(source, product => new MockProduct
        {
            Apis = product.Apis?.Select(CopyApi).ToArray()!,
            ApprovalRequired = product.ApprovalRequired,
            Groups = product.Groups?.Select(CopyGroup).ToArray()!,
            Id = product.Id,
            Name = product.Name,
            State = product.State,
            SubscriptionLimit = product.SubscriptionLimit,
            SubscriptionRequired = product.SubscriptionRequired
        });

        private static NotSupportedException Unsupported(object value) =>
            new($"Wait cannot isolate mutable gateway state of type '{value.GetType().FullName}'. " +
                "Use supported emulator snapshots or an explicit WaitBranches callback.");

        private static void ValidateComparer(IEqualityComparer<string> comparer)
        {
            var type = comparer.GetType();
            if (type != EqualityComparer<string>.Default.GetType()
                && type != StringComparer.Ordinal.GetType()
                && type != StringComparer.OrdinalIgnoreCase.GetType()
                && type != StringComparer.InvariantCulture.GetType())
            {
                throw new NotSupportedException(
                    $"Wait cannot isolate custom dictionary comparer '{type.FullName}'.");
            }
        }
    }
}

internal sealed record WaitVariableChange(string Name, bool Exists, object? Value);