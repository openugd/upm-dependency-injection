using System;
using System.Reflection;

namespace OpenUGD.Descriptions
{
    public class MethodDescription : MethodBaseDescription
    {
        private readonly MethodInfo _methodInfo;
        private Func<object, object[], object> _invoker;

        public MethodDescription(MethodInfo methodInfo, Attribute attribute, ParameterInfo[] parameters = null) : base(methodInfo, attribute, false, parameters)
        {
            _methodInfo = methodInfo;
            _invoker = methodInfo.Invoke;
        }

        public MethodInfo Info => _methodInfo;

        public virtual Type ReturnType => _methodInfo.ReturnType;

        public override MemberKind Kind => MemberKind.Method;

        public override Type Type => null;

        public override Type ProviderType => null;

        public override void SetValue(object target, object value)
        {
            throw new NotImplementedException();
        }

        public override object GetValue(object target)
        {
            throw new NotImplementedException();
        }

        public override void Apply(object target, Type targetType, IInjector injector)
        {
            var parameters = GetParameterValues(targetType, injector);
            _invoker(target, parameters);
        }
    }
}