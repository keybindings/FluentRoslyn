// using System;
// using System.Collections.Generic;
// using System.Linq;
// using Generatr.Abstractions;
// using Generatr.Builders.KeywordBuilders;
//
// namespace Generatr.Builders;
//
// internal class ClassContext(ClassBuilder @class)
// {
//     private readonly HashSet<string> _memberNames = new();
//     private readonly List<FieldBuilder> _fields = [];
//     private readonly List<PropertyBuilder> _properties = new();
//     private readonly List<MethodBuilder> _methods = new();
//
//     internal OptionalKeyword StaticBuilder { get; } = OptionalKeyword.Static;
//
//     internal OptionalKeyword PartialBuilder { get; } = OptionalKeyword.Partial;
//     internal ClassBuilder Class { get; } = @class;
//
//     #region Fields
//     internal FieldBuilder<T> AddField<T>(FieldBuilder<T> fieldBuilder)
//         => AddMember(fieldBuilder, _fields);
//
//     internal FieldBuilder<T> RemoveField<T>(FieldBuilder<T> fieldBuilder)
//         => RemoveMember(fieldBuilder, _fields);
//
//     internal IEnumerable<FieldBuilder> GetFields() => GetMembers(_fields);
//
//     #endregion
//
//     internal PropertyBuilder<T> AddProperty<T>(PropertyBuilder<T> propertyBuilder)
//         => AddMember(propertyBuilder, _properties);
//
//     internal PropertyBuilder<T> RemoveProperty<T>(PropertyBuilder<T> propertyBuilder)
//         => RemoveMember(propertyBuilder, _properties);
//
//     internal MethodBuilder AddMethod(MethodBuilder methodBuilder)
//         => AddMember(methodBuilder, _methods);
//
//     internal MethodBuilder RemoveMethod(MethodBuilder methodBuilder)
//         => RemoveMember(methodBuilder, _methods);
//
//     private T AddMember<T, TList>(T builder, ICollection<TList> collection) where T : INamedBuilder, TList where TList : INamedBuilder
//     {
//         if(!_memberNames.Add(builder.Name)) throw new Exception($"Cannot use member name: {builder.Name}, name is already taken.");
//         collection.Add(builder);
//         return builder;
//     }
//
//     private T RemoveMember<T, TList>(T builder, ICollection<TList> collection) where T : INamedBuilder, TList where TList : INamedBuilder
//     {
//         if (!_memberNames.Remove(builder.Name)) throw new Exception($"Cannot remove member name: {builder.Name}, name does not exist.");
//         collection.Remove(builder);
//         return builder;
//     }
//
//     private static IEnumerable<T> GetMembers<T>(IEnumerable<T> members) where T : IAccessModifier, INamedBuilder
//         => members.OrderByDescending(x => x.AccessModifier.AccessabilityLevel).ThenBy(x => x.Name);
// }