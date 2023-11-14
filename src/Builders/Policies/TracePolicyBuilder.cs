namespace Mielek.Builders.Policies
{
    using System.Collections.Immutable;

    using Mielek.Generators.Attributes;
    using Mielek.Model.Expressions;
    using Mielek.Model.Policies;

    [GenerateBuilderSetters]
    public partial class TracePolicyBuilder
    {
        private string? _source;
        private IExpression<string>? _message;
        private TraceSeverity? _severity;

        [IgnoreBuilderField]
        private ImmutableList<TraceMetadata>.Builder? _metadatas;

        public TracePolicyBuilder Metadata(Action<TraceMetadataBuilder> configurator)
        {
             var builder = new TraceMetadataBuilder();
            configurator(builder);
            (_metadatas ??= ImmutableList.CreateBuilder<TraceMetadata>()).Add(builder.Build());
            return this;
        }

        public TracePolicy Build()
        {
            if (_source == null) throw new NullReferenceException();
            if (_message == null) throw new NullReferenceException();

            return new TracePolicy(_source, _message, _severity, _metadatas?.ToImmutable());
        }
    }


    [GenerateBuilderSetters]
    public partial class TraceMetadataBuilder
    {
        private string? _name;
        private IExpression<string>? _value;

        public TraceMetadata Build()
        {
            if (_name == null) throw new NullReferenceException();
            if (_value == null) throw new NullReferenceException();

            return new TraceMetadata(_name, _value);
        }
    }
}

namespace Mielek.Builders
{
    using Mielek.Builders.Policies;

    public partial class PolicySectionBuilder
    {
        public PolicySectionBuilder Trace(Action<TracePolicyBuilder> configurator)
        {
            var builder = new TracePolicyBuilder();
            configurator(builder);
            _sectionPolicies.Add(builder.Build());
            return this;
        }
    }
}