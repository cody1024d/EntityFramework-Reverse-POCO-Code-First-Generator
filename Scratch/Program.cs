﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Entity.Infrastructure.Pluralization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Scratch
{
    public class Program
    {
        static void Main()
        {
            GeneratedTextTransformation.Inflector.PluralizationService = new SpanishPluralizationService();
            GeneratedTextTransformation.Inflector.PluralizationService = null;
            GeneratedTextTransformation.Inflector.PluralizationService = new EnglishPluralizationService();

            using (var sw = new StreamWriter(@"c:\fred.txt"))
            {
                var x = new GeneratedTextTransformation();
                var tables = x.LoadTables();
                foreach (var table in tables.Where(t => !t.IsMapping))
                {
                    Console.WriteLine(table.NameHumanCase);
                    sw.WriteLine(table.NameHumanCase);

                    foreach (var col in table.Columns)
                    {
                        if (!string.IsNullOrWhiteSpace(col.Entity))
                            Console.WriteLine("  " + col.Entity);

                        if (!string.IsNullOrWhiteSpace(col.EntityFk))
                        {
                            Console.WriteLine("  " + col.EntityFk);
                            sw.WriteLine("  " + col.EntityFk);
                        }
                    }
                    if (table.Columns.Count > 0)
                        Console.WriteLine();

                    foreach (var rp in table.ReverseNavigationProperty)
                    {
                        Console.WriteLine("  " + rp);
                        sw.WriteLine("  " + rp);
                    }
                    if (table.ReverseNavigationProperty.Count > 0)
                        Console.WriteLine();

                    Console.WriteLine("  // Config");
                    foreach (var rc in table.MappingConfiguration)
                    {
                        Console.WriteLine("  " + rc);
                        sw.WriteLine("  " + rc);
                    }
                    if (table.MappingConfiguration.Count > 0)
                        Console.WriteLine();

                    foreach (var col in table.Columns)
                    {
                        if (!string.IsNullOrWhiteSpace(col.Config))
                            Console.WriteLine("  " + col.Config);
                    }
                    if (table.Columns.Count > 0)
                        Console.WriteLine();

                    var fks = table.Columns.Where(col => !string.IsNullOrWhiteSpace(col.ConfigFk)).ToList();
                    if (fks.Count > 0)
                        Console.WriteLine("  // FK's");
                    foreach (var col in fks)
                    {
                        Console.WriteLine("  " + col.ConfigFk);
                        sw.WriteLine("  " + col.ConfigFk);
                    }
                    Console.WriteLine();
                    sw.WriteLine();
                }
            }
        }
    }

    public class GeneratedTextTransformation
    {
        private void WriteLine(string s) { }
        private void WriteLine(string s, object b) { }
        private void WriteLine(string s, object b, object c) { }
        private void Warning(string s) { }
        private string ZapPassword(string s) { return s; }

        // Settings
        private const string ProviderName = "System.Data.SqlClient";
        string ConnectionString = "Data Source=(local);Initial Catalog=northwind;Integrated Security=True;Application Name=EntityFramework Reverse POCO Generator";   // Uses last connection string in config if not specified

        // Use this when testing SQL Server Compact 4.0
        //private const string ProviderName = "System.Data.SqlServerCe.4.0";
        //string ConnectionString = @"Data Source=|DataDirectory|\NorthwindSqlCe40.sdf";   // Uses last connection string in config if not specified

        string ConnectionStringName = "MyDbContext";   // Uses last connection string in config if not specified
        bool IncludeViews = true;
        string DbContextName = "MyDbContext";
        string ConfigurationClassName = "Configuration";
        string CollectionType = "List";
        string CollectionTypeNamespace = "";
        bool MakeClassesPartial = true;
        bool GenerateSeparateFiles = false;
        string FileExtension = ".cs";
        bool UseCamelCase = true;
        bool IncludeComments = true;
        bool AddWcfDataAttributes = false;
        string ExtraWcfDataContractAttributes = "";
        string SchemaName = null;
        static bool DisableGeographyTypes = false;
        bool PrependSchemaName = true;
        Regex TableFilterExclude = null;
        Regex TableFilterInclude = null;
        Regex TableRenameFilter = null;
        string TableRenameReplacement = "";
        string[] ConfigFilenameSearchOrder = null;
        private string _connectionString = "";
        private string _providerName = "";
        private string _configFilePath = "";

        // Settings to allow selective code generation
        [Flags]
        private enum Elements
        {
            Poco = 1,
            Context = 2,
            UnitOfWork = 4,
            PocoConfiguration = 8
        };
        Elements ElementsToGenerate = Elements.Poco | Elements.Context | Elements.UnitOfWork | Elements.PocoConfiguration;
        string PocoNamespace, ContextNamespace, UnitOfWorkNamespace, PocoConfigurationNamespace = "";

        // Settings to allow TargetFramework checks
        static string TargetFrameworkVersion = null;
        Func<string, bool> IsSupportedFrameworkVersion = (string frameworkVersion) =>
        {
            if (!string.IsNullOrEmpty(TargetFrameworkVersion))
            {
                return String.Compare(TargetFrameworkVersion, frameworkVersion) >= 0;
            }
            return true;
        };

        static string[] ReservedKeywords = new string[]
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
            "checked", "class", "const", "continue", "decimal", "default", "delegate", "do",
            "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", 
            "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
            "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator",
            "out", "override", "params", "private", "protected", "public", "readonly", "ref",
            "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
            "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
            "unchecked", "unsafe", "ushort", "using", "virtual", "volatile", "void", "while"
        };
        private static readonly Regex RxCleanUp = new Regex(@"[^\w\d_]", RegexOptions.Compiled);

        private static readonly Func<string, string> CleanUp = (str) =>
        {
            // Replace punctuation and symbols in variable names as these are not allowed.
            int len = str.Length;
            if (len == 0)
                return str;
            var sb = new StringBuilder();
            bool replacedCharacter = false;
            for (int n = 0; n < len; ++n)
            {
                char c = str[n];
                if (c != '_' && (char.IsSymbol(c) || char.IsPunctuation(c)))
                {
                    int ascii = c;
                    sb.AppendFormat("{0}", ascii);
                    replacedCharacter = true;
                    continue;
                }
                sb.Append(c);
            }
            if (replacedCharacter)
                str = sb.ToString();

            // Remove non alphanumerics
            str = RxCleanUp.Replace(str, "");
            if (char.IsDigit(str[0]))
                str = "C" + str;

            return str;
        };



        private static string CheckNullable(Column col)
        {
            string result = "";
            if (col.IsNullable &&
                col.PropertyType != "byte[]" &&
                col.PropertyType != "string" &&
                col.PropertyType != "Microsoft.SqlServer.Types.SqlGeography" &&
                col.PropertyType != "Microsoft.SqlServer.Types.SqlGeometry" &&
                col.PropertyType != "System.Data.Entity.Spatial.DbGeography" &&
                col.PropertyType != "System.Data.Entity.Spatial.DbGeometry")
                result = "?";
            return result;
        }









        public Tables LoadTables()
        {
            WriteLine("// This file was automatically generated.");
            WriteLine("// Do not make changes directly to this file - edit the template instead.");
            WriteLine("// ");
            WriteLine("// The following connection settings were used to generate this file");
            WriteLine("// ");
            WriteLine("//     Connection String Name: \"{0}\"", ConnectionStringName);
            WriteLine("//     Connection String:      \"{0}\"", ZapPassword(ConnectionString));
            WriteLine("");

            DbProviderFactory factory;
            try
            {
                factory = DbProviderFactories.GetFactory(ProviderName);
            }
            catch (Exception x)
            {
                string error = x.Message.Replace("\r\n", "\n").Replace("\n", " ");
                Warning(string.Format("Failed to load provider \"{0}\" - {1}", ProviderName, error));
                WriteLine("");
                WriteLine("// -----------------------------------------------------------------------------------------");
                WriteLine("// Failed to load provider \"{0}\" - {1}", ProviderName, error);
                WriteLine("// -----------------------------------------------------------------------------------------");
                WriteLine("");
                return new Tables();
            }

            try
            {
                using (DbConnection conn = factory.CreateConnection())
                {
                    conn.ConnectionString = ConnectionString;
                    conn.Open();

                    if (conn.GetType().Name == "SqlCeConnection")
                        PrependSchemaName = false;

                    var reader = new SqlServerSchemaReader(conn, factory) { Outer = this };
                    var tables = reader.ReadSchema(TableFilterExclude, UseCamelCase, PrependSchemaName, IncludeComments, TableRenameFilter, TableRenameReplacement);
                    tables.SetPrimaryKeys();

                    // Remove unrequired tables/views
                    for (int i = tables.Count - 1; i >= 0; i--)
                    {
                        if (SchemaName != null && String.Compare(tables[i].Schema, SchemaName, StringComparison.OrdinalIgnoreCase) != 0)
                        {
                            tables.RemoveAt(i);
                            continue;
                        }
                        if (!IncludeViews && tables[i].IsView)
                        {
                            tables.RemoveAt(i);
                            continue;
                        }
                        if (TableFilterInclude != null && !TableFilterInclude.IsMatch(tables[i].Name))
                        {
                            tables.RemoveAt(i);
                            continue;
                        }
                        if (!tables[i].IsView && string.IsNullOrEmpty(tables[i].PrimaryKeyNameHumanCase()))
                        {
                            tables.RemoveAt(i);
                        }
                    }

                    // Must be done in this order
                    var fkList = reader.ReadForeignKeys(TableRenameFilter, TableRenameReplacement);
                    reader.IdentifyForeignKeys(fkList, tables);
                    reader.ProcessForeignKeys(fkList, tables, UseCamelCase, PrependSchemaName, CollectionType, true, IncludeComments);
                    tables.IdentifyMappingTables(fkList, UseCamelCase, PrependSchemaName, CollectionType, true, IncludeComments);

                    tables.ResetNavigationProperties();
                    reader.ProcessForeignKeys(fkList, tables, UseCamelCase, PrependSchemaName, CollectionType, false, IncludeComments);
                    tables.IdentifyMappingTables(fkList, UseCamelCase, PrependSchemaName, CollectionType, false, IncludeComments);

                    // Remove views that only consist of all nullable fields.
                    // I.e. they do not contain any primary key, and therefore cannot be used by EF
                    for (int i = tables.Count - 1; i >= 0; i--)
                    {
                        if (string.IsNullOrEmpty(tables[i].PrimaryKeyNameHumanCase()))
                        {
                            tables.RemoveAt(i);
                        }
                    }

                    conn.Close();
                    return tables;
                }
            }
            catch (Exception x)
            {
                string error = x.Message.Replace("\r\n", "\n").Replace("\n", " ");
                Warning(string.Format("Failed to read database schema - {0}", error));
                WriteLine("");
                WriteLine("// -----------------------------------------------------------------------------------------");
                WriteLine("// Failed to read database schema - {0}", error);
                WriteLine("// -----------------------------------------------------------------------------------------");
                WriteLine("");
                return new Tables();
            }
        }

        public enum Relationship
        {
            OneToOne,
            OneToMany,
            ManyToOne,
            ManyToMany,
        }

        public static Relationship CalcRelationship(Table pkTable, Table fkTable, Column fkCol, Column pkCol)
        {
            bool fkTableSinglePrimaryKey = (fkTable.PrimaryKeys.Count() == 1);
            bool pkTableSinglePrimaryKey = (pkTable.PrimaryKeys.Count() == 1);

            // 1:1
            if (fkCol.IsPrimaryKey && pkCol.IsPrimaryKey && fkTableSinglePrimaryKey && pkTableSinglePrimaryKey)
                return Relationship.OneToOne;

            // 1:n
            if (fkCol.IsPrimaryKey && !pkCol.IsPrimaryKey && fkTableSinglePrimaryKey)
                return Relationship.OneToMany;

            // n:1
            if (!fkCol.IsPrimaryKey && pkCol.IsPrimaryKey && pkTableSinglePrimaryKey)
                return Relationship.ManyToOne;

            // n:n
            return Relationship.ManyToMany;
        }

        #region Nested type: Column

        public class Column
        {
            public string Name;
            public int DateTimePrecision;
            public string Default;
            public int MaxLength;
            public int Precision;
            public string PropertyName;
            public string PropertyNameHumanCase;
            public string PropertyType;
            public int Scale;
            public int Ordinal;

            public bool IsIdentity;
            public bool IsNullable;
            public bool IsPrimaryKey;
            public bool IsStoreGenerated;
            public bool IsRowVersion;

            public string Config;
            public string ConfigFk;
            public string Entity;
            public string EntityFk;

            private void SetupEntity(bool includeComments)
            {
                string comments;
                if (includeComments)
                {
                    comments = " // " + Name;
                    if (IsPrimaryKey)
                        comments += " (Primary key)";
                }
                else
                {
                    comments = string.Empty;
                }
                Entity = string.Format("public {0}{1} {2} {3}{4}", PropertyType, CheckNullable(this), PropertyNameHumanCase, IsStoreGenerated ? "{ get; internal set; }" : "{ get; set; }", comments);
            }

            private void SetupConfig()
            {
                bool hasDatabaseGeneratedOption = false;
                string propertyType = PropertyType.ToLower();
                switch (propertyType)
                {
                    case "long":
                    case "short":
                    case "int":
                    case "double":
                    case "float":
                    case "decimal":
                    case "string":
                        hasDatabaseGeneratedOption = true;
                        break;
                }
                string databaseGeneratedOption = string.Empty;
                if (hasDatabaseGeneratedOption)
                {
                    if (IsIdentity)
                        databaseGeneratedOption = ".HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity)";
                    if (IsStoreGenerated)
                        databaseGeneratedOption = ".HasDatabaseGeneratedOption(DatabaseGeneratedOption.Computed)";
                    if (IsPrimaryKey && !IsIdentity && !IsStoreGenerated)
                        databaseGeneratedOption = ".HasDatabaseGeneratedOption(DatabaseGeneratedOption.None)";
                }
                Config = string.Format("Property(x => x.{0}).HasColumnName(\"{1}\"){2}{3}{4}{5}{6};", PropertyNameHumanCase, Name,
                                            (IsNullable) ? ".IsOptional()" : ".IsRequired()",
                                            (MaxLength > 0) ? ".HasMaxLength(" + MaxLength + ")" : string.Empty,
                                            (Scale > 0) ? ".HasPrecision(" + Precision + "," + Scale + ")" : string.Empty,
                                            (IsRowVersion) ? ".IsFixedLength().IsRowVersion()" : string.Empty,
                                            databaseGeneratedOption);
            }

            public void SetupEntityAndConfig(bool includeComments)
            {
                SetupEntity(includeComments);
                SetupConfig();
            }

            public void CleanUpDefault()
            {
                if (string.IsNullOrEmpty(Default))
                    return;

                while (Default.First() == '(' && Default.Last() == ')' && Default.Length > 2)
                {
                    Default = Default.Substring(1, Default.Length - 2);
                }

                if (Default.First() == '\'' && Default.Last() == '\'' && Default.Length >= 2)
                    Default = string.Format("\"{0}\"", Default.Substring(1, Default.Length - 2));

                switch (PropertyType.ToLower())
                {
                    case "bool":
                        Default = (Default == "0") ? "false" : "true";
                        break;

                    case "string":
                    case "datetime":
                    case "timespan":
                    case "datetimeoffset":
                        if (Default.First() != '"')
                            Default = string.Format("\"{0}\"", Default);
                        if (Default.Contains('\\'))
                            Default = "@" + Default;
                        break;

                    case "long":
                    case "short":
                    case "int":
                    case "double":
                    case "float":
                    case "decimal":
                    case "byte":
                    case "guid":
                        if (Default.First() == '\"' && Default.Last() == '\"' && Default.Length > 2)
                            Default = Default.Substring(1, Default.Length - 2);
                        break;

                    case "byte[]":
                    case "System.Data.Entity.Spatial.DbGeography":
                    case "System.Data.Entity.Spatial.DbGeometry":
                        Default = string.Empty;
                        break;
                }

                if (string.IsNullOrWhiteSpace(Default))
                    return;

                // Validate default
                switch (PropertyType.ToLower())
                {
                    case "long":
                        long l;
                        if (!long.TryParse(Default, out l))
                            Default = string.Empty;
                        break;

                    case "short":
                        short s;
                        if (!short.TryParse(Default, out s))
                            Default = string.Empty;
                        break;

                    case "int":
                        int i;
                        if (!int.TryParse(Default, out i))
                            Default = string.Empty;
                        break;

                    case "datetime":
                        DateTime dt;
                        if (!DateTime.TryParse(Default, out dt))
                            Default = Default.ToLower().Contains("getdate()") ? "System.DateTime.Now" : string.Empty;
                        else
                            Default = string.Format("System.DateTime.Parse({0})", Default);
                        break;

                    case "datetimeoffset":
                        DateTimeOffset dto;
                        if (!DateTimeOffset.TryParse(Default, out dto))
                            Default = Default.ToLower().Contains("sysdatetimeoffset()") ? "System.DateTimeOffset.Now" : string.Empty;
                        else
                            Default = string.Format("System.DateTimeOffset.Parse({0})", Default);
                        break;

                    case "timespan":
                        TimeSpan ts;
                        if (!TimeSpan.TryParse(Default, out ts))
                            Default = string.Empty;
                        else
                            Default = string.Format("System.TimeSpan.Parse({0})", Default);
                        break;

                    case "double":
                        double d;
                        if (!double.TryParse(Default, out d))
                            Default = string.Empty;
                        break;

                    case "float":
                        float f;
                        if (!float.TryParse(Default, out f))
                            Default = string.Empty;
                        break;

                    case "decimal":
                        decimal dec;
                        if (!decimal.TryParse(Default, out dec))
                            Default = string.Empty;
                        else
                            Default += "m";
                        break;

                    case "byte":
                        byte b;
                        if (!byte.TryParse(Default, out b))
                            Default = string.Empty;
                        break;

                    case "bool":
                        bool x;
                        if (!bool.TryParse(Default, out x))
                            Default = string.Empty;
                        break;

                    case "guid":
                        if (Default.ToLower() == "newid()" || Default.ToLower() == "newsequentialid()")
                            Default = "System.Guid.NewGuid()";
                        else
                            Default = string.Format("Guid.Parse(\"{0}\")", Default);
                        break;
                }
            }
        }

        #endregion

        #region Nested type: Inflector

        /// <summary>
        /// Summary for the Inflector class
        /// </summary>
        public static class Inflector
        {
            static public IPluralizationService PluralizationService = null;

            /// <summary>
            /// Makes the plural.
            /// </summary>
            /// <param name="word">The word.</param>
            /// <returns></returns>
            public static string MakePlural(string word)
            {
                try
                {
                    return (PluralizationService == null) ? word : PluralizationService.Pluralize(word);
                }
                catch (Exception)
                {
                    return word;
                }
            }

            /// <summary>
            /// Makes the singular.
            /// </summary>
            /// <param name="word">The word.</param>
            /// <returns></returns>
            public static string MakeSingular(string word)
            {
                try
                {
                    return (PluralizationService == null) ? word : PluralizationService.Singularize(word);
                }
                catch (Exception)
                {
                    return word;
                }
            }

            /// <summary>
            /// Converts the string to title case.
            /// </summary>
            /// <param name="word">The word.</param>
            /// <returns></returns>
            public static string ToTitleCase(string word)
            {
                string s = Regex.Replace(ToHumanCase(AddUnderscores(word)), @"\b([a-z])", match => match.Captures[0].Value.ToUpper());
                bool digit = false;
                string a = string.Empty;
                foreach (char c in s)
                {
                    if (Char.IsDigit(c))
                    {
                        digit = true;
                        a = a + c;
                    }
                    else
                    {
                        if (digit && Char.IsLower(c))
                            a = a + Char.ToUpper(c);
                        else
                            a = a + c;
                        digit = false;
                    }
                }
                return a;
            }

            /// <summary>
            /// Converts the string to human case.
            /// </summary>
            /// <param name="lowercaseAndUnderscoredWord">The lowercase and underscored word.</param>
            /// <returns></returns>
            public static string ToHumanCase(string lowercaseAndUnderscoredWord)
            {
                return MakeInitialCaps(Regex.Replace(lowercaseAndUnderscoredWord, @"_", " "));
            }


            /// <summary>
            /// Adds the underscores.
            /// </summary>
            /// <param name="pascalCasedWord">The pascal cased word.</param>
            /// <returns></returns>
            public static string AddUnderscores(string pascalCasedWord)
            {
                return
                    Regex.Replace(Regex.Replace(Regex.Replace(pascalCasedWord, @"([A-Z]+)([A-Z][a-z])", "$1_$2"), @"([a-z\d])([A-Z])", "$1_$2"), @"[-\s]", "_").ToLower();
            }

            /// <summary>
            /// Makes the initial caps.
            /// </summary>
            /// <param name="word">The word.</param>
            /// <returns></returns>
            public static string MakeInitialCaps(string word)
            {
                return String.Concat(word.Substring(0, 1).ToUpper(), word.Substring(1).ToLower());
            }
        }

        #endregion

        #region Nested type: SchemaReader

        private abstract class SchemaReader
        {
            protected readonly DbCommand Cmd;

            protected SchemaReader(DbConnection connection, DbProviderFactory factory)
            {
                Cmd = factory.CreateCommand();
                if (Cmd != null)
                    Cmd.Connection = connection;
            }

            public GeneratedTextTransformation Outer;
            public abstract Tables ReadSchema(Regex tableFilterExclude, bool useCamelCase, bool prependSchemaName, bool includeComments, Regex tableRenameFilter, string tableRenameReplacement);
            public abstract List<ForeignKey> ReadForeignKeys(Regex tableRenameFilter, string tableRenameReplacement);
            public abstract void ProcessForeignKeys(List<ForeignKey> fkList, Tables tables, bool useCamelCase, bool prependSchemaName, string collectionType, bool checkForFkNameClashes, bool includeComments);
            public abstract void IdentifyForeignKeys(List<ForeignKey> fkList, Tables tables);

            protected void WriteLine(string o)
            {
                Outer.WriteLine(o);
            }
        }

        #endregion

        private class SqlServerSchemaReader : SchemaReader
        {
            private const string TableSQL = @"
SELECT  [Extent1].[SchemaName],
        [Extent1].[Name] AS TableName,
        [Extent1].[TABLE_TYPE] AS TableType,
        [UnionAll1].[Ordinal],
        [UnionAll1].[Name] AS ColumnName,
        [UnionAll1].[IsNullable],
        [UnionAll1].[TypeName],
        ISNULL([UnionAll1].[MaxLength],0) AS MaxLength,
        ISNULL([UnionAll1].[Precision], 0) AS Precision,
        ISNULL([UnionAll1].[Default], '') AS [Default],
        ISNULL([UnionAll1].[DateTimePrecision], '') AS [DateTimePrecision],
        ISNULL([UnionAll1].[Scale], 0) AS Scale,
        [UnionAll1].[IsIdentity],
        [UnionAll1].[IsStoreGenerated],
        CASE WHEN ([Project5].[C2] IS NULL) THEN CAST(0 AS BIT)
             ELSE [Project5].[C2]
        END AS PrimaryKey
FROM    (
         SELECT QUOTENAME(TABLE_SCHEMA) + QUOTENAME(TABLE_NAME) [Id],
                TABLE_SCHEMA [SchemaName],
                TABLE_NAME [Name],
                TABLE_TYPE
         FROM   INFORMATION_SCHEMA.TABLES
         WHERE  TABLE_TYPE IN ('BASE TABLE', 'VIEW')
        ) AS [Extent1]
        INNER JOIN (
                    SELECT  [Extent2].[Id] AS [Id],
                            [Extent2].[Name] AS [Name],
                            [Extent2].[Ordinal] AS [Ordinal],
                            [Extent2].[IsNullable] AS [IsNullable],
                            [Extent2].[TypeName] AS [TypeName],
                            [Extent2].[MaxLength] AS [MaxLength],
                            [Extent2].[Precision] AS [Precision],
                            [Extent2].[Default],
                            [Extent2].[DateTimePrecision] AS [DateTimePrecision],
                            [Extent2].[Scale] AS [Scale],
                            [Extent2].[IsIdentity] AS [IsIdentity],
                            [Extent2].[IsStoreGenerated] AS [IsStoreGenerated],
                            0 AS [C1],
                            [Extent2].[ParentId] AS [ParentId]
                    FROM    (
                             SELECT QUOTENAME(c.TABLE_SCHEMA) + QUOTENAME(c.TABLE_NAME) + QUOTENAME(c.COLUMN_NAME) [Id],
                                    QUOTENAME(c.TABLE_SCHEMA) + QUOTENAME(c.TABLE_NAME) [ParentId],
                                    c.COLUMN_NAME [Name],
                                    c.ORDINAL_POSITION [Ordinal],
                                    CAST(CASE c.IS_NULLABLE
                                           WHEN 'YES' THEN 1
                                           WHEN 'NO' THEN 0
                                           ELSE 0
                                         END AS BIT) [IsNullable],
                                    CASE WHEN c.DATA_TYPE IN ('varchar', 'nvarchar', 'varbinary')
                                              AND c.CHARACTER_MAXIMUM_LENGTH = -1 THEN c.DATA_TYPE + '(max)'
                                         ELSE c.DATA_TYPE
                                    END AS [TypeName],
                                    c.CHARACTER_MAXIMUM_LENGTH [MaxLength],
                                    CAST(c.NUMERIC_PRECISION AS INTEGER) [Precision],
                                    CAST(c.DATETIME_PRECISION AS INTEGER) [DateTimePrecision],
                                    CAST(c.NUMERIC_SCALE AS INTEGER) [Scale],
                                    c.COLLATION_CATALOG [CollationCatalog],
                                    c.COLLATION_SCHEMA [CollationSchema],
                                    c.COLLATION_NAME [CollationName],
                                    c.CHARACTER_SET_CATALOG [CharacterSetCatalog],
                                    c.CHARACTER_SET_SCHEMA [CharacterSetSchema],
                                    c.CHARACTER_SET_NAME [CharacterSetName],
                                    CAST(0 AS BIT) AS [IsMultiSet],
                                    CAST(COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)), c.COLUMN_NAME, 'IsIdentity') AS BIT) AS [IsIdentity],
                                    CAST(COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)), c.COLUMN_NAME, 'IsComputed')
                                    | CASE WHEN c.DATA_TYPE = 'timestamp' THEN 1
                                           ELSE 0
                                      END AS BIT) AS [IsStoreGenerated],
                                    c.COLUMN_DEFAULT AS [Default]
                             FROM   INFORMATION_SCHEMA.COLUMNS c
                                    INNER JOIN INFORMATION_SCHEMA.TABLES t
                                        ON c.TABLE_CATALOG = t.TABLE_CATALOG
                                           AND c.TABLE_SCHEMA = t.TABLE_SCHEMA
                                           AND c.TABLE_NAME = t.TABLE_NAME
                                           AND t.TABLE_TYPE IN ('BASE TABLE', 'VIEW')
                            ) AS [Extent2]
                    UNION ALL
                    SELECT  [Extent3].[Id] AS [Id],
                            [Extent3].[Name] AS [Name],
                            [Extent3].[Ordinal] AS [Ordinal],
                            [Extent3].[IsNullable] AS [IsNullable],
                            [Extent3].[TypeName] AS [TypeName],
                            [Extent3].[MaxLength] AS [MaxLength],
                            [Extent3].[Precision] AS [Precision],
                            [Extent3].[Default],
                            [Extent3].[DateTimePrecision] AS [DateTimePrecision],
                            [Extent3].[Scale] AS [Scale],
                            [Extent3].[IsIdentity] AS [IsIdentity],
                            [Extent3].[IsStoreGenerated] AS [IsStoreGenerated],
                            6 AS [C1],
                            [Extent3].[ParentId] AS [ParentId]
                    FROM    (
                             SELECT QUOTENAME(c.TABLE_SCHEMA) + QUOTENAME(c.TABLE_NAME) + QUOTENAME(c.COLUMN_NAME) [Id],
                                    QUOTENAME(c.TABLE_SCHEMA) + QUOTENAME(c.TABLE_NAME) [ParentId],
                                    c.COLUMN_NAME [Name],
                                    c.ORDINAL_POSITION [Ordinal],
                                    CAST(CASE c.IS_NULLABLE
                                           WHEN 'YES' THEN 1
                                           WHEN 'NO' THEN 0
                                           ELSE 0
                                         END AS BIT) [IsNullable],
                                    CASE WHEN c.DATA_TYPE IN ('varchar', 'nvarchar', 'varbinary')
                                              AND c.CHARACTER_MAXIMUM_LENGTH = -1 THEN c.DATA_TYPE + '(max)'
                                         ELSE c.DATA_TYPE
                                    END AS [TypeName],
                                    c.CHARACTER_MAXIMUM_LENGTH [MaxLength],
                                    CAST(c.NUMERIC_PRECISION AS INTEGER) [Precision],
                                    CAST(c.DATETIME_PRECISION AS INTEGER) AS [DateTimePrecision],
                                    CAST(c.NUMERIC_SCALE AS INTEGER) [Scale],
                                    c.COLLATION_CATALOG [CollationCatalog],
                                    c.COLLATION_SCHEMA [CollationSchema],
                                    c.COLLATION_NAME [CollationName],
                                    c.CHARACTER_SET_CATALOG [CharacterSetCatalog],
                                    c.CHARACTER_SET_SCHEMA [CharacterSetSchema],
                                    c.CHARACTER_SET_NAME [CharacterSetName],
                                    CAST(0 AS BIT) AS [IsMultiSet],
                                    CAST(COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)), c.COLUMN_NAME, 'IsIdentity') AS BIT) AS [IsIdentity],
                                    CAST(COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)), c.COLUMN_NAME, 'IsComputed')
                                    | CASE WHEN c.DATA_TYPE = 'timestamp' THEN 1
                                           ELSE 0
                                      END AS BIT) AS [IsStoreGenerated],
                                    c.COLUMN_DEFAULT [Default]
                             FROM   INFORMATION_SCHEMA.COLUMNS c
                                    INNER JOIN INFORMATION_SCHEMA.VIEWS v
                                        ON c.TABLE_CATALOG = v.TABLE_CATALOG
                                           AND c.TABLE_SCHEMA = v.TABLE_SCHEMA
                                           AND c.TABLE_NAME = v.TABLE_NAME
                             WHERE  NOT (
                                         v.TABLE_SCHEMA = 'dbo'
                                         AND v.TABLE_NAME IN ('syssegments', 'sysconstraints')
                                         AND SUBSTRING(CAST(SERVERPROPERTY('productversion') AS VARCHAR(20)), 1, 1) = 8
                                        )
                            ) AS [Extent3]
                   ) AS [UnionAll1]
            ON (0 = [UnionAll1].[C1])
               AND ([Extent1].[Id] = [UnionAll1].[ParentId])
        LEFT OUTER JOIN (
                         SELECT [UnionAll2].[Id] AS [C1],
                                CAST(1 AS BIT) AS [C2]
                         FROM   (
                                 SELECT QUOTENAME(tc.CONSTRAINT_SCHEMA) + QUOTENAME(tc.CONSTRAINT_NAME) [Id],
                                        QUOTENAME(tc.TABLE_SCHEMA) + QUOTENAME(tc.TABLE_NAME) [ParentId],
                                        tc.CONSTRAINT_NAME [Name],
                                        tc.CONSTRAINT_TYPE [ConstraintType],
                                        CAST(CASE tc.IS_DEFERRABLE
                                               WHEN 'NO' THEN 0
                                               ELSE 1
                                             END AS BIT) [IsDeferrable],
                                        CAST(CASE tc.INITIALLY_DEFERRED
                                               WHEN 'NO' THEN 0
                                               ELSE 1
                                             END AS BIT) [IsInitiallyDeferred]
                                 FROM   INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                                 WHERE  tc.TABLE_NAME IS NOT NULL
                                ) AS [Extent4]
                                INNER JOIN (
                                            SELECT  7 AS [C1],
                                                    [Extent5].[ConstraintId] AS [ConstraintId],
                                                    [Extent6].[Id] AS [Id]
                                            FROM    (
                                                     SELECT QUOTENAME(CONSTRAINT_SCHEMA) + QUOTENAME(CONSTRAINT_NAME) [ConstraintId],
                                                            QUOTENAME(TABLE_SCHEMA) + QUOTENAME(TABLE_NAME) + QUOTENAME(COLUMN_NAME) [ColumnId]
                                                     FROM   INFORMATION_SCHEMA.KEY_COLUMN_USAGE
                                                    ) AS [Extent5]
                                                    INNER JOIN (
                                                                SELECT  QUOTENAME(c.TABLE_SCHEMA) + QUOTENAME(c.TABLE_NAME) + QUOTENAME(c.COLUMN_NAME) [Id],
                                                                        QUOTENAME(c.TABLE_SCHEMA) + QUOTENAME(c.TABLE_NAME) [ParentId],
                                                                        c.COLUMN_NAME [Name],
                                                                        c.ORDINAL_POSITION [Ordinal],
                                                                        CAST(CASE c.IS_NULLABLE
                                                                               WHEN 'YES' THEN 1
                                                                               WHEN 'NO' THEN 0
                                                                               ELSE 0
                                                                             END AS BIT) [IsNullable],
                                                                        CASE WHEN c.DATA_TYPE IN ('varchar', 'nvarchar', 'varbinary')
                                                                                  AND c.CHARACTER_MAXIMUM_LENGTH = -1 THEN c.DATA_TYPE + '(max)'
                                                                             ELSE c.DATA_TYPE
                                                                        END AS [TypeName],
                                                                        c.CHARACTER_MAXIMUM_LENGTH [MaxLength],
                                                                        CAST(c.NUMERIC_PRECISION AS INTEGER) [Precision],
                                                                        CAST(c.DATETIME_PRECISION AS INTEGER) [DateTimePrecision],
                                                                        CAST(c.NUMERIC_SCALE AS INTEGER) [Scale],
                                                                        c.COLLATION_CATALOG [CollationCatalog],
                                                                        c.COLLATION_SCHEMA [CollationSchema],
                                                                        c.COLLATION_NAME [CollationName],
                                                                        c.CHARACTER_SET_CATALOG [CharacterSetCatalog],
                                                                        c.CHARACTER_SET_SCHEMA [CharacterSetSchema],
                                                                        c.CHARACTER_SET_NAME [CharacterSetName],
                                                                        CAST(0 AS BIT) AS [IsMultiSet],
                                                                        CAST(COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)),
                                                                                            c.COLUMN_NAME, 'IsIdentity') AS BIT) AS [IsIdentity],
                                                                        CAST(COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)),
                                                                                            c.COLUMN_NAME, 'IsComputed')
                                                                        | CASE WHEN c.DATA_TYPE = 'timestamp' THEN 1
                                                                               ELSE 0
                                                                          END AS BIT) AS [IsStoreGenerated],
                                                                        c.COLUMN_DEFAULT AS [Default]
                                                                FROM    INFORMATION_SCHEMA.COLUMNS c
                                                                        INNER JOIN INFORMATION_SCHEMA.TABLES t
                                                                            ON c.TABLE_CATALOG = t.TABLE_CATALOG
                                                                               AND c.TABLE_SCHEMA = t.TABLE_SCHEMA
                                                                               AND c.TABLE_NAME = t.TABLE_NAME
                                                                               AND t.TABLE_TYPE IN ('BASE TABLE', 'VIEW')
                                                               ) AS [Extent6]
                                                        ON [Extent6].[Id] = [Extent5].[ColumnId]
                                            UNION ALL
                                            SELECT  11 AS [C1],
                                                    [Extent7].[ConstraintId] AS [ConstraintId],
                                                    [Extent8].[Id] AS [Id]
                                            FROM    (
                                                     SELECT CAST( NULL AS NVARCHAR (1)) [ConstraintId], CAST( NULL AS NVARCHAR (MAX)) [ColumnId] WHERE 1= 2
                                                    ) AS [Extent7]
                                                    INNER JOIN (
                                                                SELECT  QUOTENAME(c.TABLE_SCHEMA) + QUOTENAME(c.TABLE_NAME) + QUOTENAME(c.COLUMN_NAME) [Id],
                                                                        QUOTENAME(c.TABLE_SCHEMA) + QUOTENAME(c.TABLE_NAME) [ParentId],
                                                                        c.COLUMN_NAME [Name],
                                                                        c.ORDINAL_POSITION [Ordinal],
                                                                        CAST(CASE c.IS_NULLABLE
                                                                               WHEN 'YES' THEN 1
                                                                               WHEN 'NO' THEN 0
                                                                               ELSE 0
                                                                             END AS BIT) [IsNullable],
                                                                        CASE WHEN c.DATA_TYPE IN ('varchar', 'nvarchar', 'varbinary')
                                                                                  AND c.CHARACTER_MAXIMUM_LENGTH = -1 THEN c.DATA_TYPE + '(max)'
                                                                             ELSE c.DATA_TYPE
                                                                        END AS [TypeName],
                                                                        c.CHARACTER_MAXIMUM_LENGTH [MaxLength],
                                                                        CAST(c.NUMERIC_PRECISION AS INTEGER) [Precision],
                                                                        CAST(c.DATETIME_PRECISION AS INTEGER) AS [DateTimePrecision],
                                                                        CAST(c.NUMERIC_SCALE AS INTEGER) [Scale],
                                                                        c.COLLATION_CATALOG [CollationCatalog],
                                                                        c.COLLATION_SCHEMA [CollationSchema],
                                                                        c.COLLATION_NAME [CollationName],
                                                                        c.CHARACTER_SET_CATALOG [CharacterSetCatalog],
                                                                        c.CHARACTER_SET_SCHEMA [CharacterSetSchema],
                                                                        c.CHARACTER_SET_NAME [CharacterSetName],
                                                                        CAST(0 AS BIT) AS [IsMultiSet],
                                                                        CAST(COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)),
                                                                                            c.COLUMN_NAME, 'IsIdentity') AS BIT) AS [IsIdentity],
                                                                        CAST(COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)),
                                                                                            c.COLUMN_NAME, 'IsComputed')
                                                                        | CASE WHEN c.DATA_TYPE = 'timestamp' THEN 1
                                                                               ELSE 0
                                                                          END AS BIT) AS [IsStoreGenerated],
                                                                        c.COLUMN_DEFAULT [Default]
                                                                FROM    INFORMATION_SCHEMA.COLUMNS c
                                                                        INNER JOIN INFORMATION_SCHEMA.VIEWS v
                                                                            ON c.TABLE_CATALOG = v.TABLE_CATALOG
                                                                               AND c.TABLE_SCHEMA = v.TABLE_SCHEMA
                                                                               AND c.TABLE_NAME = v.TABLE_NAME
                                                                WHERE   NOT (
                                                                             v.TABLE_SCHEMA = 'dbo'
                                                                             AND v.TABLE_NAME IN ('syssegments', 'sysconstraints')
                                                                             AND SUBSTRING(CAST(SERVERPROPERTY('productversion') AS VARCHAR(20)), 1, 1) = 8
                                                                            )
                                                               ) AS [Extent8]
                                                        ON [Extent8].[Id] = [Extent7].[ColumnId]
                                           ) AS [UnionAll2]
                                    ON (7 = [UnionAll2].[C1])
                                       AND ([Extent4].[Id] = [UnionAll2].[ConstraintId])
                         WHERE  [Extent4].[ConstraintType] = N'PRIMARY KEY'
                        ) AS [Project5]
            ON [UnionAll1].[Id] = [Project5].[C1]
WHERE   NOT ([Extent1].[Name] IN ('EdmMetadata', '__MigrationHistory'))";

            private const string ForeignKeySQL = @"
SELECT DISTINCT
        FK.TABLE_NAME AS FK_Table,
        FK.COLUMN_NAME AS FK_Column,
        PK.TABLE_NAME AS PK_Table,
        PK.COLUMN_NAME AS PK_Column,
        FK.CONSTRAINT_NAME AS Constraint_Name,
        FK.TABLE_SCHEMA AS fkSchema,
        PK.TABLE_SCHEMA AS pkSchema,
        PT.COLUMN_NAME AS primarykey
FROM    INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS AS C
        INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS FK
            ON FK.CONSTRAINT_CATALOG = C.CONSTRAINT_CATALOG
               AND FK.CONSTRAINT_SCHEMA = C.CONSTRAINT_SCHEMA
               AND FK.CONSTRAINT_NAME = C.CONSTRAINT_NAME
        INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS PK
            ON PK.CONSTRAINT_CATALOG = C.UNIQUE_CONSTRAINT_CATALOG
               AND PK.CONSTRAINT_SCHEMA = C.UNIQUE_CONSTRAINT_SCHEMA
               AND PK.CONSTRAINT_NAME = C.UNIQUE_CONSTRAINT_NAME
               AND PK.ORDINAL_POSITION = FK.ORDINAL_POSITION
        INNER JOIN (
                    SELECT  i1.TABLE_NAME,
                            i2.COLUMN_NAME
                    FROM    INFORMATION_SCHEMA.TABLE_CONSTRAINTS i1
                            INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE i2
                                ON i1.CONSTRAINT_NAME = i2.CONSTRAINT_NAME
                    WHERE   i1.CONSTRAINT_TYPE = 'PRIMARY KEY'
                   ) PT
            ON PT.TABLE_NAME = PK.TABLE_NAME
WHERE   PT.COLUMN_NAME = PK.COLUMN_NAME
ORDER BY FK.TABLE_NAME,
        FK.COLUMN_NAME";

            private const string TableSQLCE = @"
SELECT  '' AS SchemaName,
		c.TABLE_NAME AS TableName,
		'BASE TABLE' AS TableType,
		c.ORDINAL_POSITION AS Ordinal,
		c.COLUMN_NAME AS ColumnName,
		CAST(CASE WHEN c.IS_NULLABLE = N'YES' THEN 1
		ELSE 0 END AS bit) AS IsNullable,
		c.DATA_TYPE AS TypeName,
		CASE WHEN c.CHARACTER_MAXIMUM_LENGTH IS NOT NULL THEN c.CHARACTER_MAXIMUM_LENGTH
		ELSE 0 END AS MaxLength,
		CASE WHEN c.NUMERIC_PRECISION IS NOT NULL THEN c.NUMERIC_PRECISION
		ELSE 0 END AS Precision,
		c.COLUMN_DEFAULT AS [Default],
		CASE WHEN c.DATA_TYPE = N'datetime' THEN 0
		ELSE 0 END AS DateTimePrecision,
		CASE WHEN c.DATA_TYPE = N'datetime' THEN 0
		WHEN c.NUMERIC_SCALE IS NOT NULL THEN c.NUMERIC_SCALE
		ELSE 0 END AS Scale,
		CAST(CASE WHEN c.AUTOINC_INCREMENT > 0 THEN 1
		ELSE 0 END AS bit) AS IsIdentity,
		CAST(CASE WHEN c.DATA_TYPE = N'rowversion' THEN 1
		ELSE 0 END AS bit) AS IsStoreGenerated,
		CAST(CASE WHEN u.TABLE_NAME IS NULL THEN 0
		ELSE 1 END AS bit) AS PrimaryKey				
FROM INFORMATION_SCHEMA.COLUMNS c
		INNER JOIN INFORMATION_SCHEMA.TABLES t 
			ON c.TABLE_NAME = t.TABLE_NAME
		LEFT JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS cons 
			ON cons.TABLE_NAME = c.TABLE_NAME 
		LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS u 
			ON cons.CONSTRAINT_NAME = u.CONSTRAINT_NAME AND u.TABLE_NAME = c.TABLE_NAME AND u.COLUMN_NAME = c.COLUMN_NAME
WHERE t.TABLE_TYPE <> N'SYSTEM TABLE' AND cons.CONSTRAINT_TYPE = 'PRIMARY KEY'
ORDER BY c.TABLE_NAME, c.COLUMN_NAME, c.ORDINAL_POSITION;";

            private const string ForeignKeySQLCE = @"
SELECT DISTINCT
        FK.TABLE_NAME AS FK_Table,
        FK.COLUMN_NAME AS FK_Column,
        PK.TABLE_NAME AS PK_Table,
        PK.COLUMN_NAME AS PK_Column,
        FK.CONSTRAINT_NAME AS Constraint_Name,
        '' AS fkSchema,
        '' AS pkSchema,
        PT.COLUMN_NAME AS primarykey
FROM    INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS AS C
        INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS FK
            ON FK.CONSTRAINT_NAME = C.CONSTRAINT_NAME
        INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS PK
            ON PK.CONSTRAINT_NAME = C.UNIQUE_CONSTRAINT_NAME
               AND PK.ORDINAL_POSITION = FK.ORDINAL_POSITION
        INNER JOIN (
                    SELECT  i1.TABLE_NAME,
                            i2.COLUMN_NAME
                    FROM    INFORMATION_SCHEMA.TABLE_CONSTRAINTS i1
                            INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE i2
                                ON i1.CONSTRAINT_NAME = i2.CONSTRAINT_NAME
                    WHERE   i1.CONSTRAINT_TYPE = 'PRIMARY KEY'
                   ) PT
            ON PT.TABLE_NAME = PK.TABLE_NAME
WHERE   PT.COLUMN_NAME = PK.COLUMN_NAME
ORDER BY FK.TABLE_NAME,
        FK.COLUMN_NAME;";

            public SqlServerSchemaReader(DbConnection connection, DbProviderFactory factory)
                : base(connection, factory)
            {
            }

            public override Tables ReadSchema(Regex tableFilterExclude, bool useCamelCase, bool prependSchemaName, bool includeComments, Regex tableRenameFilter, string tableRenameReplacement)
            {
                var result = new Tables();
                if (Cmd == null)
                    return result;

                Cmd.CommandText = TableSQL;
                if (Cmd.GetType().Name == "SqlCeCommand")
                    Cmd.CommandText = TableSQLCE;

                using (DbDataReader rdr = Cmd.ExecuteReader())
                {
                    var rxClean = new Regex("^(event|Equals|GetHashCode|GetType|ToString|repo|Save|IsNew|Insert|Update|Delete|Exists|SingleOrDefault|Single|First|FirstOrDefault|Fetch|Page|Query)$");
                    var lastTable = string.Empty;
                    Table table = null;
                    while (rdr.Read())
                    {
                        string tableName = rdr["TableName"].ToString().Trim();
                        if (tableFilterExclude != null && tableFilterExclude.IsMatch(tableName))
                            continue;

                        if (lastTable != tableName || table == null)
                        {
                            // The data from the database is not sorted
                            string schema = rdr["SchemaName"].ToString().Trim();
                            table = result.Find(x => x.Name == tableName && x.Schema == schema);
                            if (table == null)
                            {
                                table = new Table
                                {
                                    Name = tableName,
                                    Schema = schema,
                                    IsView = String.Compare(rdr["TableType"].ToString().Trim(), "View", StringComparison.OrdinalIgnoreCase) == 0,

                                    // Will be set later
                                    HasForeignKey = false,
                                    HasNullableColumns = false
                                };

                                if (tableRenameFilter != null)
                                    tableName = tableRenameFilter.Replace(tableName, tableRenameReplacement);

                                table.CleanName = CleanUp(tableName);
                                table.ClassName = Inflector.MakeSingular(table.CleanName);
                                string singular = Inflector.MakeSingular(tableName);
                                table.NameHumanCase = (useCamelCase ? Inflector.ToTitleCase(singular) : singular).Replace(" ", "").Replace("$", "");
                                if ((string.Compare(table.Schema, "dbo", StringComparison.OrdinalIgnoreCase) != 0) && prependSchemaName)
                                    table.NameHumanCase = table.Schema + "_" + table.NameHumanCase;

                                // Check for table or C# name clashes
                                if (ReservedKeywords.Contains(table.NameHumanCase) ||
                                    (useCamelCase && result.Find(x => x.NameHumanCase == table.NameHumanCase) != null))
                                {
                                    table.NameHumanCase += "1";
                                }

                                result.Add(table);
                            }
                        }

                        var col = CreateColumn(rdr, rxClean, table, useCamelCase);
                        if (col != null)
                            table.Columns.Add(col);
                    }
                }

                // Check for property name clashes in columns
                foreach (Column c in result.SelectMany(tbl => tbl.Columns.Where(c => tbl.Columns.FindAll(x => x.PropertyNameHumanCase == c.PropertyNameHumanCase).Count > 1)))
                {
                    c.PropertyNameHumanCase = c.PropertyName;
                }

                foreach (Table tbl in result)
                {
                    tbl.Columns.ForEach(x => x.SetupEntityAndConfig(includeComments));
                }

                return result;
            }

            public override List<ForeignKey> ReadForeignKeys(Regex tableRenameFilter, string tableRenameReplacement)
            {
                var fkList = new List<ForeignKey>();
                if (Cmd == null)
                    return fkList;

                Cmd.CommandText = ForeignKeySQL;
                if (Cmd.GetType().Name == "SqlCeCommand")
                    Cmd.CommandText = ForeignKeySQLCE;

                using (DbDataReader rdr = Cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        string fkTableName = rdr["FK_Table"].ToString();
                        string fkSchema = rdr["fkSchema"].ToString();
                        string pkTableName = rdr["PK_Table"].ToString();
                        string pkSchema = rdr["pkSchema"].ToString();
                        string fkColumn = rdr["FK_Column"].ToString().Replace(" ", "");
                        string pkColumn = rdr["PK_Column"].ToString().Replace(" ", "");
                        string constraintName = rdr["Constraint_Name"].ToString().Replace(" ", "");

                        string fkTableNameFiltered, pkTableNameFiltered;
                        if (tableRenameFilter != null)
                        {
                            fkTableNameFiltered = tableRenameFilter.Replace(fkTableName, tableRenameReplacement);
                            pkTableNameFiltered = tableRenameFilter.Replace(pkTableName, tableRenameReplacement);
                        }
                        else
                        {
                            fkTableNameFiltered = fkTableName;
                            pkTableNameFiltered = pkTableName;
                        }

                        fkList.Add(new ForeignKey(fkTableName, fkSchema, pkTableName, pkSchema, fkColumn, pkColumn, constraintName, fkTableNameFiltered, pkTableNameFiltered));
                    }
                }

                return fkList;
            }

            public override void ProcessForeignKeys(List<ForeignKey> fkList, Tables tables, bool useCamelCase, bool prependSchemaName, string collectionType, bool checkForFkNameClashes, bool includeComments)
            {
                var constraints = fkList.Select(x => x.ConstraintName).Distinct();
                foreach (var constraint in constraints)
                {
                    var localConstraint = constraint;
                    var foreignKeys = fkList.Where(x => x.ConstraintName == localConstraint).ToList();
                    var foreignKey = foreignKeys.First();

                    Table fkTable = tables.GetTable(foreignKey.FkTableName, foreignKey.FkSchema);
                    if (fkTable == null || fkTable.IsMapping || !fkTable.HasForeignKey)
                        continue;

                    Table pkTable = tables.GetTable(foreignKey.PkTableName, foreignKey.PkSchema);
                    if (pkTable == null || pkTable.IsMapping)
                        continue;

                    var fkCols = foreignKeys.Select(x => fkTable.Columns.Find(n => n.PropertyName == x.FkColumn)).Where(x => x != null).OrderBy(o => o.Ordinal).ToList();
                    var pkCols = foreignKeys.Select(x => pkTable.Columns.Find(n => n.PropertyName == x.PkColumn)).Where(x => x != null).OrderBy(o => o.Ordinal).ToList();

                    var fkCol = fkCols.First();
                    var pkCol = pkCols.First();

                    var relationship = CalcRelationship(pkTable, fkTable, fkCol, pkCol);

                    string pkTableHumanCase = foreignKey.PkTableHumanCase(useCamelCase, prependSchemaName);
                    string pkPropName = fkTable.GetUniqueColumnPropertyName(pkTableHumanCase, foreignKey, useCamelCase, checkForFkNameClashes, true);
                    bool fkMakePropNameSingular = (relationship == Relationship.OneToOne);
                    string fkPropName = pkTable.GetUniqueColumnPropertyName(fkTable.NameHumanCase, foreignKey, useCamelCase, checkForFkNameClashes, fkMakePropNameSingular);

                    fkCol.EntityFk = string.Format("public virtual {0} {1} {2}{3}", pkTableHumanCase, pkPropName, "{ get; set; }", includeComments ? " // " + foreignKey.ConstraintName : string.Empty);

                    string manyToManyMapping;
                    if (foreignKeys.Count > 1)
                        manyToManyMapping = string.Format("c => new {{ {0} }}", string.Join(", ", fkCols.Select(x => "c." + x.PropertyNameHumanCase).ToArray()));
                    else
                        manyToManyMapping = string.Format("c => c.{0}", fkCol.PropertyNameHumanCase);

                    fkCol.ConfigFk = string.Format("{0};{1}", GetRelationship(relationship, fkCol, pkCol, pkPropName, fkPropName, manyToManyMapping), includeComments ? " // " + foreignKey.ConstraintName : string.Empty);
                    pkTable.AddReverseNavigation(relationship, pkTableHumanCase, fkTable, fkPropName, string.Format("{0}.{1}", fkTable.Name, foreignKey.ConstraintName), collectionType, includeComments);
                }
            }

            public override void IdentifyForeignKeys(List<ForeignKey> fkList, Tables tables)
            {
                foreach (var foreignKey in fkList)
                {
                    Table fkTable = tables.GetTable(foreignKey.FkTableName, foreignKey.FkSchema);
                    if (fkTable == null)
                        continue;   // Could be filtered out

                    Table pkTable = tables.GetTable(foreignKey.PkTableName, foreignKey.PkSchema);
                    if (pkTable == null)
                        continue;   // Could be filtered out

                    Column fkCol = fkTable.Columns.Find(n => n.PropertyName == foreignKey.FkColumn);
                    if (fkCol == null)
                        continue;   // Could not find fk column

                    Column pkCol = pkTable.Columns.Find(n => n.PropertyName == foreignKey.PkColumn);
                    if (pkCol == null)
                        continue;   // Could not find pk column

                    fkTable.HasForeignKey = true;
                }
            }

            private static string GetRelationship(Relationship relationship, Column fkCol, Column pkCol, string pkPropName, string fkPropName, string manyToManyMapping)
            {
                return string.Format("Has{0}(a => a.{1}){2}", GetHasMethod(fkCol, pkCol), pkPropName, GetWithMethod(relationship, fkCol, fkPropName, manyToManyMapping));
            }

            // HasOptional
            // HasRequired
            // HasMany
            private static string GetHasMethod(Column fkCol, Column pkCol)
            {
                if (pkCol.IsPrimaryKey)
                    return fkCol.IsNullable ? "Optional" : "Required";

                return "Many";
            }

            // WithOptional
            // WithRequired
            // WithMany
            // WithRequiredPrincipal
            // WithRequiredDependent
            private static string GetWithMethod(Relationship relationship, Column fkCol, string fkPropName, string manyToManyMapping)
            {
                switch (relationship)
                {
                    case Relationship.OneToOne:
                        return string.Format(".WithOptional(b => b.{0})", fkPropName);

                    case Relationship.OneToMany:
                        return string.Format(".WithRequiredDependent(b => b.{0})", fkPropName);

                    case Relationship.ManyToOne:
                        return string.Format(".WithMany(b => b.{0}).HasForeignKey(c => c.{1})", fkPropName, fkCol.PropertyNameHumanCase);

                    case Relationship.ManyToMany:
                        return string.Format(".WithMany(b => b.{0}).HasForeignKey({1})", fkPropName, manyToManyMapping);

                    default:
                        throw new ArgumentOutOfRangeException("relationship");
                }
            }

            private static Column CreateColumn(IDataRecord rdr, Regex rxClean, Table table, bool useCamelCase)
            {
                if (rdr == null)
                    throw new ArgumentNullException("rdr");

                string typename = rdr["TypeName"].ToString().Trim();
                var scale = (int)rdr["Scale"];
                var precision = (int)rdr["Precision"];

                var col = new Column
                {
                    Name = rdr["ColumnName"].ToString().Trim(),
                    PropertyType = GetPropertyType(typename, scale, precision),
                    MaxLength = (int)rdr["MaxLength"],
                    Precision = precision,
                    Default = rdr["Default"].ToString().Trim(),
                    DateTimePrecision = (int)rdr["DateTimePrecision"],
                    Scale = scale,
                    Ordinal = (int)rdr["Ordinal"],
                    IsIdentity = rdr["IsIdentity"].ToString().Trim().ToLower() == "true",
                    IsNullable = rdr["IsNullable"].ToString().Trim().ToLower() == "true",
                    IsStoreGenerated = rdr["IsStoreGenerated"].ToString().Trim().ToLower() == "true",
                    IsPrimaryKey = rdr["PrimaryKey"].ToString().Trim().ToLower() == "true"
                };

                col.IsRowVersion = col.IsStoreGenerated && !col.IsNullable && typename == "timestamp";
                if (col.IsRowVersion)
                    col.MaxLength = 8;

                col.CleanUpDefault();
                col.PropertyName = CleanUp(col.Name);
                col.PropertyName = rxClean.Replace(col.PropertyName, "_$1");

                // Make sure property name doesn't clash with class name
                if (col.PropertyName == table.NameHumanCase)
                    col.PropertyName = col.PropertyName + "_";

                col.PropertyNameHumanCase = (useCamelCase ? Inflector.ToTitleCase(col.PropertyName) : col.PropertyName).Replace(" ", "");
                if (col.PropertyNameHumanCase == string.Empty)
                    col.PropertyNameHumanCase = col.PropertyName;

                // Make sure property name doesn't clash with class name
                if (col.PropertyNameHumanCase == table.NameHumanCase)
                    col.PropertyNameHumanCase = col.PropertyNameHumanCase + "_";

                if (char.IsDigit(col.PropertyNameHumanCase[0]))
                    col.PropertyNameHumanCase = "_" + col.PropertyNameHumanCase;

                if (CheckNullable(col) != string.Empty)
                    table.HasNullableColumns = true;

                // If PropertyType is empty, return null. Most likely ignoring a column due to legacy (such as OData not supporting spatial types)
                if (string.IsNullOrEmpty(col.PropertyType))
                    return null;

                return col;
            }

            private static string GetPropertyType(string sqlType, int scale, int precision)
            {
                string sysType = "string";
                switch (sqlType)
                {
                    case "bigint":
                        sysType = "long";
                        break;
                    case "smallint":
                        sysType = "short";
                        break;
                    case "int":
                        sysType = "int";
                        break;
                    case "uniqueidentifier":
                        sysType = "Guid";
                        break;
                    case "smalldatetime":
                    case "datetime":
                    case "datetime2":
                    case "date":
                        sysType = "DateTime";
                        break;
                    case "datetimeoffset":
                        sysType = "DateTimeOffset";
                        break;
                    case "time":
                        sysType = "TimeSpan";
                        break;
                    case "float":
                        sysType = "double";
                        break;
                    case "real":
                        sysType = "float";
                        break;
                    case "numeric":
                    case "decimal":
                        if (scale == 0)
                        {
                            if (precision >= 15)
                                sysType = "Int64";
                            else if (precision >= 8)
                                sysType = "Int32";
                            else
                                sysType = "Int16";
                        }
                        else
                        {
                            sysType = "Decimal";
                        }
                        break;
                    case "smallmoney":
                    case "money":
                        sysType = "decimal";
                        break;
                    case "tinyint":
                        sysType = "byte";
                        break;
                    case "bit":
                        sysType = "bool";
                        break;
                    case "image":
                    case "binary":
                    case "varbinary":
                    case "varbinary(max)":
                    case "timestamp":
                        sysType = "byte[]";
                        break;
                    case "geography":
                        if (DisableGeographyTypes)
                            sysType = "";
                        else
                            sysType = "System.Data.Entity.Spatial.DbGeography";
                        break;
                    case "geometry":
                        if (DisableGeographyTypes)
                            sysType = "";
                        else
                            sysType = "System.Data.Entity.Spatial.DbGeometry";
                        break;
                }
                return sysType;
            }
        }

        public class ForeignKey
        {
            public string FkTableName { get; private set; }
            public string FkTableNameFiltered { get; private set; }
            public string FkSchema { get; private set; }
            public string PkTableName { get; private set; }
            public string PkTableNameFiltered { get; private set; }
            public string PkSchema { get; private set; }
            public string FkColumn { get; private set; }
            public string PkColumn { get; private set; }
            public string ConstraintName { get; private set; }

            public ForeignKey(string fkTableName, string fkSchema, string pkTableName, string pkSchema, string fkColumn, string pkColumn, string constraintName, string fkTableNameFiltered, string pkTableNameFiltered)
            {
                ConstraintName = constraintName;
                PkColumn = pkColumn;
                FkColumn = fkColumn;
                PkSchema = pkSchema;
                PkTableName = pkTableName;
                FkSchema = fkSchema;
                FkTableName = fkTableName;
                FkTableNameFiltered = fkTableNameFiltered;
                PkTableNameFiltered = pkTableNameFiltered;
            }

            public string PkTableHumanCase(bool useCamelCase, bool prependSchemaName)
            {
                string singular = Inflector.MakeSingular(PkTableNameFiltered);
                string pkTableHumanCase = (useCamelCase ? Inflector.ToTitleCase(singular) : singular).Replace(" ", "").Replace("$", "");
                if (string.Compare(PkSchema, "dbo", StringComparison.OrdinalIgnoreCase) != 0 && prependSchemaName)
                    pkTableHumanCase = PkSchema + "_" + pkTableHumanCase;
                return pkTableHumanCase;
            }
        }

        public class Table
        {
            public string Name;
            public string NameHumanCase;
            public string Schema;
            public string Type;
            public string ClassName;
            public string CleanName;
            public bool IsMapping;
            public bool IsView;
            public bool HasForeignKey;
            public bool HasNullableColumns;

            public List<Column> Columns;
            public List<string> ReverseNavigationProperty;
            public List<string> MappingConfiguration;
            public List<string> ReverseNavigationCtor;
            public List<string> ReverseNavigationUniquePropName;
            public List<string> ReverseNavigationUniquePropNameClashes;

            public Table()
            {
                Columns = new List<Column>();
                ResetNavigationProperties();
                ReverseNavigationUniquePropNameClashes = new List<string>();
            }

            public void ResetNavigationProperties()
            {
                MappingConfiguration = new List<string>();
                ReverseNavigationProperty = new List<string>();
                ReverseNavigationCtor = new List<string>();
                ReverseNavigationUniquePropName = new List<string>();
            }

            public IEnumerable<Column> PrimaryKeys
            {
                get { return Columns.Where(x => x.IsPrimaryKey).ToList(); }
            }

            public string PrimaryKeyNameHumanCase()
            {
                var data = PrimaryKeys.Select(x => "x." + x.PropertyNameHumanCase).ToList();
                int n = data.Count();
                if (n == 0)
                    return string.Empty;
                if (n == 1)
                    return "x => " + data.First();
                // More than one primary key
                return string.Format("x => new {{ {0} }}", string.Join(", ", data));
            }

            public Column this[string columnName]
            {
                get { return GetColumn(columnName); }
            }

            public Column GetColumn(string columnName)
            {
                return Columns.SingleOrDefault(x => String.Compare(x.Name, columnName, StringComparison.OrdinalIgnoreCase) == 0);
            }

            public string GetUniqueColumnPropertyName(string tableNameHumanCase, ForeignKey foreignKey, bool useCamelCase, bool checkForFkNameClashes, bool makeSingular)
            {
                if (ReverseNavigationUniquePropName.Count == 0)
                {
                    ReverseNavigationUniquePropName.Add(NameHumanCase);
                    ReverseNavigationUniquePropName.AddRange(Columns.Select(c => c.PropertyNameHumanCase));
                }

                if (!makeSingular)
                    tableNameHumanCase = Inflector.MakePlural(tableNameHumanCase);

                if (checkForFkNameClashes && ReverseNavigationUniquePropName.Contains(tableNameHumanCase) && !ReverseNavigationUniquePropNameClashes.Contains(tableNameHumanCase))
                    ReverseNavigationUniquePropNameClashes.Add(tableNameHumanCase); // Name clash

                // Try without appending foreign key name
                if (!ReverseNavigationUniquePropNameClashes.Contains(tableNameHumanCase) && !ReverseNavigationUniquePropName.Contains(tableNameHumanCase))
                {
                    ReverseNavigationUniquePropName.Add(tableNameHumanCase);
                    return tableNameHumanCase;
                }

                // Append foreign key name
                string fkName = (useCamelCase ? Inflector.ToTitleCase(foreignKey.FkColumn) : foreignKey.FkColumn);
                string col = tableNameHumanCase + "_" + fkName.Replace(" ", "").Replace("$", "");

                if (checkForFkNameClashes && ReverseNavigationUniquePropName.Contains(col) && !ReverseNavigationUniquePropNameClashes.Contains(col))
                    ReverseNavigationUniquePropNameClashes.Add(col); // Name clash

                if (!ReverseNavigationUniquePropNameClashes.Contains(col) && !ReverseNavigationUniquePropName.Contains(col))
                {
                    ReverseNavigationUniquePropName.Add(col);
                    return col;
                }

                for (int n = 1; n < 99; ++n)
                {
                    col = tableNameHumanCase + n;

                    if (ReverseNavigationUniquePropName.Contains(col))
                        continue;

                    ReverseNavigationUniquePropName.Add(col);
                    return col;
                }

                // Give up
                return tableNameHumanCase;
            }

            public void AddReverseNavigation(Relationship relationship, string fkName, Table fkTable, string propName, string constraint, string collectionType, bool includeComments)
            {
                switch (relationship)
                {
                    case Relationship.OneToOne:
                        ReverseNavigationProperty.Add(string.Format("public virtual {0} {1} {{ get; set; }}{2}", fkTable.NameHumanCase, propName, includeComments ? " // " + constraint : string.Empty));
                        break;

                    case Relationship.OneToMany:
                        ReverseNavigationProperty.Add(string.Format("public virtual {0} {1} {{ get; set; }}{2}", fkTable.NameHumanCase, propName, includeComments ? " // " + constraint : string.Empty));
                        break;

                    case Relationship.ManyToOne:
                        ReverseNavigationProperty.Add(string.Format("public virtual ICollection<{0}> {1} {{ get; set; }}{2}", fkTable.NameHumanCase, propName, includeComments ? " // " + constraint : string.Empty));
                        ReverseNavigationCtor.Add(string.Format("{0} = new {1}<{2}>();", propName, collectionType, fkTable.NameHumanCase));
                        break;

                    case Relationship.ManyToMany:
                        ReverseNavigationProperty.Add(string.Format("public virtual ICollection<{0}> {1} {{ get; set; }}{2}", fkTable.NameHumanCase, propName, includeComments ? " // Many to many mapping" : string.Empty));
                        ReverseNavigationCtor.Add(string.Format("{0} = new {1}<{2}>();", propName, collectionType, fkTable.NameHumanCase));
                        break;

                    default:
                        throw new ArgumentOutOfRangeException("relationship");
                }
            }

            public void AddMappingConfiguration(ForeignKey left, ForeignKey right, bool useCamelCase, bool prependSchemaName)
            {
                var leftTableHumanCase = Inflector.MakePlural(left.PkTableHumanCase(useCamelCase, prependSchemaName));
                var rightTableHumanCase = Inflector.MakePlural(right.PkTableHumanCase(useCamelCase, prependSchemaName));

                MappingConfiguration.Add(string.Format(@"HasMany(t => t.{0}).WithMany(t => t.{1}).Map(m => 
            {{
                m.ToTable(""{2}"");
                m.MapLeftKey(""{3}"");
                m.MapRightKey(""{4}"");
            }});", rightTableHumanCase, leftTableHumanCase, left.FkTableName, left.FkColumn, right.FkColumn));
            }

            public void SetPrimaryKeys()
            {
                if (PrimaryKeys.Any())
                    return; // Table has at least one primary key

                // This table is not allowed in EntityFramework as it does not have a primary key.
                // Therefore generate a composite key from all non-null fields.
                foreach (var col in Columns.Where(x => !x.IsNullable))
                {
                    col.IsPrimaryKey = true;
                }
            }

            public void IdentifyMappingTable(List<ForeignKey> fkList, Tables tables, bool useCamelCase, bool prependSchemaName, string collectionType, bool checkForFkNameClashes, bool includeComments)
            {
                IsMapping = false;

                // Must have only 2 columns to be a mapping table
                if (Columns.Count != 2)
                    return;

                // All columns must be primary keys
                if (PrimaryKeys.Count() != 2)
                    return;

                // No columns should be nullable
                if (Columns.Any(x => x.IsNullable))
                    return;

                // Find the foreign keys for this table
                var foreignKeys = fkList.Where(x =>
                                               String.Compare(x.FkTableName, Name, StringComparison.OrdinalIgnoreCase) == 0 &&
                                               String.Compare(x.FkSchema, Schema, StringComparison.OrdinalIgnoreCase) == 0)
                                        .ToList();

                // Each column must have a foreign key, therefore check column and foreign key counts match
                if (foreignKeys.Select(x => x.FkColumn).Distinct().Count() != 2)
                    return;

                ForeignKey left = foreignKeys[0];
                ForeignKey right = foreignKeys[1];

                Table leftTable = tables.GetTable(left.PkTableName, left.PkSchema);
                if (leftTable == null)
                    return;

                Table rightTable = tables.GetTable(right.PkTableName, right.PkSchema);
                if (rightTable == null)
                    return;

                leftTable.AddMappingConfiguration(left, right, useCamelCase, prependSchemaName);

                IsMapping = true;
                rightTable.AddReverseNavigation(Relationship.ManyToMany, rightTable.NameHumanCase, leftTable,
                                               rightTable.GetUniqueColumnPropertyName(leftTable.NameHumanCase, left, useCamelCase, checkForFkNameClashes, false), null, collectionType, includeComments);

                leftTable.AddReverseNavigation(Relationship.ManyToMany, leftTable.NameHumanCase, rightTable,
                                                leftTable.GetUniqueColumnPropertyName(rightTable.NameHumanCase, right, useCamelCase, checkForFkNameClashes, false), null, collectionType, includeComments);
            }
        }

        public class Tables : List<Table>
        {
            public Table GetTable(string tableName, string schema)
            {
                return this.SingleOrDefault(x =>
                    String.Compare(x.Name, tableName, StringComparison.OrdinalIgnoreCase) == 0 &&
                    String.Compare(x.Schema, schema, StringComparison.OrdinalIgnoreCase) == 0);
            }

            public void SetPrimaryKeys()
            {
                foreach (var tbl in this)
                {
                    tbl.SetPrimaryKeys();
                }
            }

            public void IdentifyMappingTables(List<ForeignKey> fkList, bool useCamelCase, bool prependSchemaName, string collectionType, bool checkForFkNameClashes, bool includeComments)
            {
                foreach (var tbl in this.Where(x => x.HasForeignKey))
                {
                    tbl.IdentifyMappingTable(fkList, this, useCamelCase, prependSchemaName, collectionType, checkForFkNameClashes, includeComments);
                }
            }

            public void ResetNavigationProperties()
            {
                foreach (var tbl in this)
                {
                    tbl.ResetNavigationProperties();
                }
            }
        }
    }
}