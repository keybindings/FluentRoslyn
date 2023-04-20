using System;
using System.Text;
using Generatr.Enums;

namespace Generatr.Builders
{
    public class ClassBuilder : NamedBuilder
    {
        internal ClassBuilder(NamespaceBuilder @namespace, string name) : base(name)
        {
            Namespace = @namespace;
        }

        public bool IsFileScopedNamespace { get; set; } = true;

        //public bool IsGeneric { get; set; }

        public NamespaceBuilder Namespace { get; }

        public StandardAccessModifier AccessModifier { get; set; } = StandardAccessModifier.Public;

        public ClassBuilder ParentType { get; set; }

        #region Sets
        public ClassBuilder BlockScopedNamespace()
            => SetThenReturn(() => IsFileScopedNamespace = false);

        public ClassBuilder SetParent(ClassBuilder type)
            => SetThenReturn(() => ParentType = type);

        public ClassBuilder SetAccessModifier(StandardAccessModifier accessModifier)
            => SetThenReturn(() => AccessModifier = accessModifier);

        #endregion


        #region Fields
        public FieldBuilder AddPublicField(ClassBuilder type, string name)
            => AddField(type, name, StandardAccessModifier.Public);
        public FieldBuilder AddPrivateField(ClassBuilder type, string name) =>
            AddField(type, name, StandardAccessModifier.Private);
        public FieldBuilder AddField(ClassBuilder type, string name, StandardAccessModifier accessModifierFlags) =>
            new(this, type, name, accessModifierFlags);

        #endregion

        //#region Properties

        ////public PropertyBuilder AddGetSetPropertyField(ClassBuilder type, string name)
        ////    => AddField(this, type, name, AccessModifierFlags.Public);

        //#endregion

        protected override string Build()
        {
            var sb = new StringBuilder();
            var tabCount = 0;

            // TODO Complete usings
            // Grab all usings from base type, fields, properties, and types used within methods

            // Build those

            // Build Namespace
            sb.Append("namespace ");
            sb.Append(Namespace);
            if (UseFileScopedNamespace)
            {
                sb.Append(';');
                sb.AppendLine();

            }
            else
            {
                sb.Append('{');
                tabCount++;
            }

            sb.AppendLine(Environment.NewLine);

            // Write all fields in order of: most protected to least protected, then alphabetical

            // Write Constructors

            // Write Properties order of: most protected to least protected, then alphabetical

            // Write Methods order of: most protected to least protected, then alphabetical

            return sb.ToString();
        }

        private ClassBuilder SetThenReturn(Action action)
        {
            action();
            return this;
        }

    }
}