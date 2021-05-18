﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DeltaStruct.Generators
{
    [Generator]
    public class StructTypeContractGenerator : ISourceGenerator
    {
        private static Dictionary<string, string> Types =
            new Dictionary<string, string>()
            {
                { "ushort", "UInt16" },
                { "short", "Int16" },
                { "uint", "UInt32" },
                { "int", "Int32" },
                { "ulong", "UInt64" },
                { "long", "Int64" },
                { "float", "Single" },
                { "double", "Double" },
            };

        public void Execute(GeneratorExecutionContext context)
        {
            var syntax = context.SyntaxReceiver as MarkedClassSyntaxReceiver;
            foreach (var classDef in syntax.Classes)
            {
                var className = classDef.Identifier.ValueText;
                context.AddSource($"{className}_serializer", GenerateSerializerSource(classDef));
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new MarkedClassSyntaxReceiver());
        }

        private string GenerateSerializerSource(ClassDeclarationSyntax classDef)
        {
            var className = classDef.Identifier.ValueText;

            var text = new StringBuilder();

            text.Append($"using DeltaStruct;");
            text.Append($"using System;");
            text.Append($"using System.IO;");

            text.Append($"namespace {(classDef.Parent as NamespaceDeclarationSyntax).Name} {{");
            text.Append($"public class G_{className}_Serializer : ISerializer<{className}> {{");

            text.Append($"static G_{className}_Serializer() {{");
            text.Append($"Serializers.Register<{className}, G_{className}_Serializer>();");

            text.Append($"}} public {className} ReadFromStream(Stream stream) {{");

            text.Append($"var inst = new {className}();");
            text.Append($"var buffer = new byte[8];");

            foreach (var member in classDef.Members)
            {
                if (member is PropertyDeclarationSyntax prop &&
                    prop.Modifiers.All(m => m.ValueText == "public") &&
                    prop.AccessorList.Accessors.Any(a => a.Keyword.ValueText == "set") &&
                    prop.AttributeLists.Single().Attributes
                    .Any(a => a.Name.ToString() == "StructMember"))
                {
                    var propName = prop.Identifier.ValueText;
                    var typeName = prop.Type.ToString();

                    switch (typeName)
                    {
                        case "byte":
                            text.Append($"inst.{propName} = stream.ReadByte();");
                            break;
                        case "sbyte":
                            text.Append($"inst.{propName} = unchecked((sbyte)stream.ReadByte());");
                            break;

                        case "ushort":
                        case "short":
                        case "uint":
                        case "int":
                        case "ulong":
                        case "long":
                        case "float":
                        case "double":
                            text.Append($"stream.Read(buffer, 0, sizeof({typeName}));");
                            text.Append($"inst.{propName} = BitConverter.To{Types[typeName]}(buffer, 0);");
                            break;
                    }
                }
            }

            text.Append($"return inst; }} public void WriteToStream({className} inst, Stream stream) {{");
            text.Append($"var buffer = new byte[8].AsSpan();");

            foreach (var member in classDef.Members)
            {
                if (member is PropertyDeclarationSyntax prop &&
                    prop.Modifiers.All(m => m.ValueText == "public") &&
                    prop.AccessorList.Accessors.Any(a => a.Keyword.ValueText == "set") &&
                    prop.AttributeLists.Single().Attributes
                    .Any(a => a.Name.ToString() == "StructMember"))
                {
                    var propName = prop.Identifier.ValueText;
                    var typeName = prop.Type.ToString();

                    switch (typeName)
                    {
                        case "byte":
                            text.Append($"stream.WriteByte(inst.{propName});");
                            break;
                        case "sbyte":
                            text.Append($"stream.WriteByte(unchecked((byte)inst.{propName}));");
                            break;

                        case "ushort":
                        case "short":
                        case "uint":
                        case "int":
                        case "ulong":
                        case "long":
                        case "float":
                        case "double":
                            text.Append($"BitConverter.TryWriteBytes(buffer, inst.{propName});");
                            text.Append($"stream.Write(buffer[0..sizeof({typeName})]);");
                            break;
                    }
                }
            }

            text.Append($"}} }} }}");

            return text.ToString();
        }

        private class MarkedClassSyntaxReceiver : ISyntaxReceiver
        {
            public HashSet<ClassDeclarationSyntax> Classes { get; }

            public MarkedClassSyntaxReceiver()
            {
                Classes = new HashSet<ClassDeclarationSyntax>();
            }

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is ClassDeclarationSyntax cds &&
                   (cds.AttributeLists.SingleOrDefault()?.Attributes
                   .Any(attr => attr.Name.ToString() == "StructType") ?? false))
                {
                    Classes.Add(cds);
                }
            }
        }
    }
}
