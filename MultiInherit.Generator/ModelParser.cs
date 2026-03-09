using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MultiInherit.Generator;

internal static class ModelParser
{
    private const string ModelAttr      = "Model";
    private const string InheritAttr    = "Inherit";
    private const string InheritsAttr   = "Inherits";
    private const string FieldAttr      = "ModelField";
    private const string ComputeAttr    = "Compute";
    private const string DependsAttr    = "Depends";
    private const string Many2oneAttr   = "Many2one";
    private const string One2manyAttr   = "One2many";
    private const string Many2manyAttr  = "Many2many";
    private const string ConstrainsAttr = "Constrains";
    private const string OnchangeAttr   = "Onchange";
    private const string SqlConstrAttr  = "SqlConstraint";
    private const string SelectionAttr  = "Selection";
    private const string DefaultAttr    = "Default";

    public static ModelDeclaration? Parse(
        INamedTypeSymbol classSymbol,
        CancellationToken ct,
        IList<Diagnostic> diagnostics)
    {
        ct.ThrowIfCancellationRequested();

        var modelAttr     = GetAttribute(classSymbol, ModelAttr);
        var inheritAttrs  = GetAttributes(classSymbol, InheritAttr);
        var inheritsAttrs = GetAttributes(classSymbol, InheritsAttr);

        if (modelAttr == null && inheritAttrs.Count == 0 && inheritsAttrs.Count == 0)
            return null;

        var location  = classSymbol.Locations.FirstOrDefault();
        var syntaxRef = classSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef?.GetSyntax(ct) is ClassDeclarationSyntax cls &&
            !cls.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
        {
            diagnostics.Add(Diagnostics.Make(Diagnostics.ClassMustBePartial, location, classSymbol.Name));
            return null;
        }

        string? modelName = modelAttr != null ? GetStringArg(modelAttr, 0) : null;

        var classicalParents = new List<string>();
        var extensionParents = new List<string>();

        foreach (var attr in inheritAttrs)
        {
            var parentName = GetStringArg(attr, 0);
            if (parentName == null) continue;
            if (modelName == null || modelName == parentName)
                extensionParents.Add(parentName);
            else
                classicalParents.Add(parentName);
        }

        if (modelName == null && extensionParents.Count > 0)
            modelName = extensionParents[0];
        if (modelName == null)
            return null;

        var ns = classSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (string.IsNullOrEmpty(ns) || ns == "<global namespace>")
        {
            diagnostics.Add(Diagnostics.Make(Diagnostics.GlobalNamespaceModel, location, modelName, classSymbol.Name));
            ns = string.Empty;
        }

        // [Inherits] delegation parents
        var delegationParents = new List<DelegationParent>();
        var existingPropNames = new HashSet<string>(
            classSymbol.GetMembers().OfType<IPropertySymbol>().Select(p => p.Name),
            StringComparer.Ordinal);

        foreach (var attr in inheritsAttrs)
        {
            var parentName = GetStringArg(attr, 0);
            if (parentName == null) continue;
            var fkName = GetNamedStringArg(attr, "ForeignKey") ?? DeriveDefaultFkName(parentName);
            if (existingPropNames.Contains(fkName))
                diagnostics.Add(Diagnostics.Make(Diagnostics.ForeignKeyCollision, location, modelName, fkName, parentName));
            delegationParents.Add(new DelegationParent(parentName, fkName));
        }

        // [SqlConstraint] class-level
        var sqlConstraints = GetAttributes(classSymbol, SqlConstrAttr)
            .Select(a => new SqlConstraintDeclaration(
                Name:    GetStringArg(a, 0) ?? string.Empty,
                Sql:     GetStringArg(a, 1) ?? string.Empty,
                Message: GetStringArg(a, 2) ?? string.Empty))
            .ToList();

        // Properties: fields, computed fields, relations
        var ownFields      = new List<FieldDeclaration>();
        var computedFields = new List<ComputedFieldDeclaration>();
        var relations      = new List<RelationDeclaration>();

        ParseProperties(classSymbol, ownFields, computedFields, relations, modelName, diagnostics);

        // Methods: [Constrains], [Onchange]
        var constraintMethods = new List<ConstraintMethodDeclaration>();
        var onchangeMethods   = new List<OnchangeMethodDeclaration>();

        ParseMethods(classSymbol, constraintMethods, onchangeMethods, modelName, diagnostics);

        return new ModelDeclaration(
            ModelName:          modelName,
            Namespace:          ns,
            ClassName:          classSymbol.Name,
            IsNewModel:         modelAttr != null,
            ClassicalParents:   classicalParents,
            ExtensionParents:   extensionParents,
            DelegationParents:  delegationParents,
            OwnFields:          ownFields,
            ComputedFields:     computedFields,
            Relations:          relations,
            ConstraintMethods:  constraintMethods,
            OnchangeMethods:    onchangeMethods,
            SqlConstraints:     sqlConstraints,
            Location:           location,
            FilePath:           location?.SourceTree?.FilePath ?? string.Empty
        );
    }

    // ── Properties ────────────────────────────────────────────────────────

    private static void ParseProperties(
        INamedTypeSymbol cls,
        List<FieldDeclaration> ownFields,
        List<ComputedFieldDeclaration> computedFields,
        List<RelationDeclaration> relations,
        string modelName,
        IList<Diagnostic> diagnostics)
    {
        var methodNames = new HashSet<string>(
            cls.GetMembers().OfType<IMethodSymbol>()
                .Where(m => !m.IsStatic && m.ReturnsVoid)
                .Select(m => m.Name),
            StringComparer.Ordinal);

        // Pour [Default] : méthodes non-statiques, par nom (on vérifie le type de retour ensuite)
        var instanceMethodsByName = cls.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && !m.IsStatic)
            .GroupBy(m => m.Name)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        foreach (var member in cls.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.IsStatic || member.IsIndexer || member.IsImplicitlyDeclared) continue;

            var propLoc    = member.Locations.FirstOrDefault();
            var typeName   = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var isNullable = member.NullableAnnotation == NullableAnnotation.Annotated;
            var fieldAttr  = GetAttribute(member, FieldAttr);

            var m2oAttr  = GetAttribute(member, Many2oneAttr);
            var o2mAttr  = GetAttribute(member, One2manyAttr);
            var m2mAttr  = GetAttribute(member, Many2manyAttr);
            var compAttr = GetAttribute(member, ComputeAttr);

            if (m2oAttr != null)
            {
                var comodel = GetStringArg(m2oAttr, 0) ?? string.Empty;
                var fkName  = GetNamedStringArg(m2oAttr, "ForeignKey") ?? member.Name + "Id";
                relations.Add(new RelationDeclaration(
                    Kind:            RelationKind.Many2one,
                    PropertyName:    member.Name,
                    ComodelName:     comodel,
                    ComodelClassName: DeriveClassName(comodel),
                    Label:           GetNamedStringArg(m2oAttr, "String") ?? (fieldAttr != null ? GetNamedStringArg(fieldAttr!, "String") : null),
                    Help:            GetNamedStringArg(m2oAttr, "Help"),
                    Required:        GetNamedBoolArg(m2oAttr, "Required"),
                    ForeignKeyName:  fkName,
                    OnDelete:        GetNamedEnumArg(m2oAttr, "OnDelete", "SetNull"),
                    InverseField:    null,
                    RelationTable:   null,
                    Column1:         null,
                    Column2:         null,
                    Location:        propLoc
                ));
            }
            else if (o2mAttr != null)
            {
                var comodel  = GetStringArg(o2mAttr, 0) ?? string.Empty;
                var inverse  = GetStringArg(o2mAttr, 1) ?? string.Empty;
                relations.Add(new RelationDeclaration(
                    Kind:            RelationKind.One2many,
                    PropertyName:    member.Name,
                    ComodelName:     comodel,
                    ComodelClassName: DeriveClassName(comodel),
                    Label:           GetNamedStringArg(o2mAttr, "String"),
                    Help:            GetNamedStringArg(o2mAttr, "Help"),
                    Required:        false,
                    ForeignKeyName:  string.Empty,
                    OnDelete:        "SetNull",
                    InverseField:    inverse,
                    RelationTable:   null,
                    Column1:         null,
                    Column2:         null,
                    Location:        propLoc
                ));
            }
            else if (m2mAttr != null)
            {
                var comodel = GetStringArg(m2mAttr, 0) ?? string.Empty;
                relations.Add(new RelationDeclaration(
                    Kind:            RelationKind.Many2many,
                    PropertyName:    member.Name,
                    ComodelName:     comodel,
                    ComodelClassName: DeriveClassName(comodel),
                    Label:           GetNamedStringArg(m2mAttr, "String"),
                    Help:            GetNamedStringArg(m2mAttr, "Help"),
                    Required:        false,
                    ForeignKeyName:  string.Empty,
                    OnDelete:        "SetNull",
                    InverseField:    null,
                    RelationTable:   GetNamedStringArg(m2mAttr, "RelationTable"),
                    Column1:         GetNamedStringArg(m2mAttr, "Column1"),
                    Column2:         GetNamedStringArg(m2mAttr, "Column2"),
                    Location:        propLoc
                ));
            }
            else if (compAttr != null)
            {
                var methodName = GetStringArg(compAttr, 0) ?? string.Empty;
                var store      = GetNamedBoolArg(compAttr, "Store");
                var dependsStr = GetNamedStringArg(compAttr, "Depends");

                if (!string.IsNullOrEmpty(methodName) && !methodNames.Contains(methodName))
                    diagnostics.Add(Diagnostics.Make(Diagnostics.ComputeMethodNotFound, propLoc,
                        member.Name, modelName, methodName));

                if (member.SetMethod is { DeclaredAccessibility: Accessibility.Public })
                    diagnostics.Add(Diagnostics.Make(Diagnostics.ComputedPropertyMustBeReadOnly, propLoc,
                        member.Name, modelName));

                // La propriété doit être déclarée partial pour que le générateur puisse l'implémenter
                if (member.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is
                    PropertyDeclarationSyntax propSyntax &&
                    !propSyntax.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                    diagnostics.Add(Diagnostics.Make(Diagnostics.ComputedPropertyMustBePartial, propLoc,
                        member.Name, modelName));

                var depends = new List<string>();
                if (dependsStr != null) depends.AddRange(dependsStr.Split(',').Select(s => s.Trim()));
                foreach (var da in GetAttributes(member, DependsAttr))
                    depends.AddRange(da.ConstructorArguments.SelectMany(a => a.Values)
                        .Select(tv => tv.Value as string).Where(s => s != null)!);

                computedFields.Add(new ComputedFieldDeclaration(
                    PropertyName:  member.Name,
                    TypeName:      typeName,
                    IsNullable:    isNullable,
                    ComputeMethod: methodName,
                    Store:         store,
                    DependsOn:     depends.Distinct().ToArray(),
                    Label:         fieldAttr != null ? GetNamedStringArg(fieldAttr, "String") : null,
                    Help:          fieldAttr != null ? GetNamedStringArg(fieldAttr, "Help") : null,
                    Location:      propLoc
                ));
            }
            else
            {
                // ── [Selection] ──────────────────────────────────────────
                var selectionAttr = GetAttribute(member, SelectionAttr);
                string[]? selectionValues = null;

                if (selectionAttr != null)
                {
                    var baseType = member.Type.SpecialType;
                    var isString = baseType == SpecialType.System_String ||
                                   (member.Type is INamedTypeSymbol { SpecialType: SpecialType.System_String });
                    if (!isString)
                        diagnostics.Add(Diagnostics.Make(Diagnostics.SelectionOnNonStringProperty,
                            propLoc, member.Name, modelName, typeName));
                    else
                        selectionValues = selectionAttr.ConstructorArguments
                            .SelectMany(a => a.Values)
                            .Select(tv => tv.Value as string)
                            .Where(s => s != null)
                            .ToArray()!;
                }

                // ── [Default] ────────────────────────────────────────────
                var defaultAttr = GetAttribute(member, DefaultAttr);
                string? defaultMethod = null;
                bool isPartialProp = false;

                if (defaultAttr != null)
                {
                    var defMethodName = GetStringArg(defaultAttr, 0);
                    if (!string.IsNullOrEmpty(defMethodName))
                    {
                        if (!instanceMethodsByName.TryGetValue(defMethodName!, out var defMethod))
                        {
                            diagnostics.Add(Diagnostics.Make(Diagnostics.DefaultMethodNotFound,
                                propLoc, member.Name, modelName, defMethodName!));
                        }
                        else
                        {
                            // Vérifie que le type de retour est compatible avec le type de la propriété
                            var retTypeName = defMethod.ReturnType
                                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                                .TrimEnd('?');
                            var propTypeName = typeName.TrimEnd('?');
                            if (retTypeName != propTypeName)
                            {
                                diagnostics.Add(Diagnostics.Make(Diagnostics.DefaultMethodNotFound,
                                    propLoc, member.Name, modelName, defMethodName!));
                            }
                            else
                            {
                                defaultMethod = defMethodName;
                                isPartialProp = member.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is
                                    PropertyDeclarationSyntax pSyn &&
                                    pSyn.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
                            }
                        }
                    }
                }

                ownFields.Add(new FieldDeclaration(
                    PropertyName:    member.Name,
                    TypeName:        typeName,
                    IsNullable:      isNullable,
                    HasSetter:       member.SetMethod != null,
                    Label:           fieldAttr != null ? GetNamedStringArg(fieldAttr, "String") : null,
                    Required:        fieldAttr != null && GetNamedBoolArg(fieldAttr, "Required"),
                    Readonly:        fieldAttr != null && GetNamedBoolArg(fieldAttr, "Readonly"),
                    Help:            fieldAttr != null ? GetNamedStringArg(fieldAttr, "Help") : null,
                    Default:         fieldAttr != null ? GetNamedStringArg(fieldAttr, "Default") : null,
                    SelectionValues: selectionValues,
                    DefaultMethod:   defaultMethod,
                    IsPartialProperty: isPartialProp
                ));
            }
        }
    }

    // ── Methods ([Constrains], [Onchange]) ────────────────────────────────

    private static void ParseMethods(
        INamedTypeSymbol cls,
        List<ConstraintMethodDeclaration> constraints,
        List<OnchangeMethodDeclaration> onchanges,
        string modelName,
        IList<Diagnostic> diagnostics)
    {
        foreach (var method in cls.GetMembers().OfType<IMethodSymbol>())
        {
            if (method.MethodKind != MethodKind.Ordinary) continue;
            var loc = method.Locations.FirstOrDefault();

            var constrainsAttr = GetAttribute(method, ConstrainsAttr);
            if (constrainsAttr != null)
            {
                // MI0007 : la méthode doit être non-statique et retourner void
                if (method.IsStatic || !method.ReturnsVoid)
                    diagnostics.Add(Diagnostics.Make(Diagnostics.ConstrainsMethodNotFound, loc, method.Name, modelName));
                else
                {
                    var fields = constrainsAttr.ConstructorArguments
                        .SelectMany(a => a.Values)
                        .Select(tv => tv.Value as string)
                        .Where(s => s != null)
                        .ToArray()!;
                    constraints.Add(new ConstraintMethodDeclaration(method.Name, fields!, loc));
                }
            }

            var onchangeAttr = GetAttribute(method, OnchangeAttr);
            if (onchangeAttr != null)
            {
                // MI0008 : la méthode doit être non-statique et retourner void
                if (method.IsStatic || !method.ReturnsVoid)
                    diagnostics.Add(Diagnostics.Make(Diagnostics.OnchangeMethodNotFound, loc, method.Name, modelName));
                else
                {
                    var fields = onchangeAttr.ConstructorArguments
                        .SelectMany(a => a.Values)
                        .Select(tv => tv.Value as string)
                        .Where(s => s != null)
                        .ToArray()!;
                    onchanges.Add(new OnchangeMethodDeclaration(method.Name, fields!, loc));
                }
            }
        }
    }

    // ── Attribute helpers ─────────────────────────────────────────────────

    private static AttributeData? GetAttribute(ISymbol sym, string shortName)
        => sym.GetAttributes().FirstOrDefault(a => MatchAttr(a, shortName));

    private static List<AttributeData> GetAttributes(ISymbol sym, string shortName)
        => sym.GetAttributes().Where(a => MatchAttr(a, shortName)).ToList();

    private static bool MatchAttr(AttributeData a, string shortName)
    {
        var n = a.AttributeClass?.Name ?? string.Empty;
        return n == shortName || n == shortName + "Attribute";
    }

    private static string? GetStringArg(AttributeData attr, int index)
        => attr.ConstructorArguments.Length > index
            ? attr.ConstructorArguments[index].Value as string : null;

    private static string? GetNamedStringArg(AttributeData attr, string name)
    {
        var kv = attr.NamedArguments.FirstOrDefault(x => x.Key == name);
        return kv.Value.Value as string;
    }

    private static bool GetNamedBoolArg(AttributeData attr, string name)
    {
        var kv = attr.NamedArguments.FirstOrDefault(x => x.Key == name);
        return kv.Value.Value is true;
    }

    private static string GetNamedEnumArg(AttributeData attr, string name, string defaultValue)
    {
        var kv = attr.NamedArguments.FirstOrDefault(x => x.Key == name);
        if (kv.Value.Value == null) return defaultValue;
        // Enum stored as int — map back to name
        return kv.Value.Value.ToString() switch
        {
            "0" => "SetNull",
            "1" => "Cascade",
            "2" => "Restrict",
            _   => defaultValue
        };
    }

    private static string DeriveDefaultFkName(string parentModelName)
    {
        var segment = parentModelName.Split('.').LastOrDefault(s => s.Length > 0);
        if (string.IsNullOrEmpty(segment)) return "ParentId";
        return char.ToUpperInvariant(segment[0]) + segment.Substring(1) + "Id";
    }

    /// <summary>"res.partner" → "ResPartner" (best-effort, resolver will fix up)</summary>
    private static string DeriveClassName(string modelName)
        => string.Concat(modelName.Split('.').Where(s => s.Length > 0)
            .Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1)));
}
