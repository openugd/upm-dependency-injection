using System;
using System.Reflection;

namespace OpenUGD.Descriptions
{
    public class ConstructorDescription : MethodBaseDescription
    {
        private Func<object[], object> _constructor;

        public ConstructorDescription(ConstructorInfo constructorInfo, Attribute attribute, ParameterInfo[] parameters = null) : base(constructorInfo,
            attribute, false, parameters)
        {
            _constructor = constructorInfo.Invoke;
        }

        public object CreateInstance(Type type, IInjector injector)
        {
            var parameters = GetParameterValues(type, injector);
            try
            {
                return _constructor(parameters);
            }
            catch (Exception exception)
            {
                throw exception;
            }
        }

        public override MemberKind Kind => MemberKind.Constructor;

        public override Type Type => null;

        public override Type ProviderType => null;
    }
}