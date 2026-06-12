namespace DatabaseAuditor.WPF.Converters;

using DatabaseAuditor.Domain.Enums;
using System.Globalization;
using System.Windows.Data;

public class EnumToStringConverter : IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (value == null) return string.Empty;

        return value switch
        {
            DatabaseType dt => FormatDatabaseType(dt),
            EnvironmentType et => FormatEnvironmentType(et),
            CompareType ct => FormatCompareType(ct),
            ComparisonScope cs => FormatComparisonScope(cs),
            ChangeType ch => FormatChangeType(ch),
            ObjectStatus os => FormatObjectStatus(os),
            _ => value.ToString() ?? string.Empty
        };
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (value is not string str) return value;

        if (targetType == typeof(DatabaseType))
            return Enum.Parse<DatabaseType>(str.Replace(" ", ""));

        if (targetType == typeof(EnvironmentType))
            return Enum.Parse<EnvironmentType>(str.Replace(" ", ""));

        if (targetType == typeof(CompareType))
            return Enum.Parse<CompareType>(str.Replace(" ", ""));

        return value;
    }

    private static string FormatDatabaseType(DatabaseType dt) => dt switch
    {
        DatabaseType.SqlServer => "SQL Server",
        DatabaseType.Oracle => "Oracle",
        DatabaseType.PostgreSql => "PostgreSQL",
        DatabaseType.MySql => "MySQL",
        DatabaseType.MariaDb => "MariaDB",
        _ => dt.ToString()
    };

    private static string FormatEnvironmentType(EnvironmentType et) => et switch
    {
        EnvironmentType.Development => "Development",
        EnvironmentType.QA => "QA",
        EnvironmentType.UAT => "UAT",
        EnvironmentType.Staging => "Staging",
        EnvironmentType.Production => "Production",
        _ => et.ToString()
    };

    private static string FormatCompareType(CompareType ct) => ct switch
    {
        CompareType.Database => "Database (Full)",
        CompareType.Table => "Tables",
        CompareType.Column => "Columns",
        CompareType.Procedure => "Stored Procedures",
        CompareType.View => "Views",
        CompareType.Function => "Functions",
        CompareType.Trigger => "Triggers",
        CompareType.Constraint => "Constraints",
        CompareType.Index => "Indexes",
        _ => ct.ToString()
    };

    private static string FormatComparisonScope(ComparisonScope scope) => scope switch
    {
        ComparisonScope.EntireDatabase => "Entire Database",
        ComparisonScope.SingleObject => "Single Object",
        ComparisonScope.MultipleObjects => "Multiple Objects",
        _ => scope.ToString()
    };

    private static string FormatChangeType(ChangeType ch) => ch switch
    {
        ChangeType.Added => "Added",
        ChangeType.Deleted => "Deleted",
        ChangeType.Modified => "Modified",
        ChangeType.Unchanged => "Unchanged",
        _ => ch.ToString()
    };

    private static string FormatObjectStatus(ObjectStatus os) => os switch
    {
        ObjectStatus.Match => "Match",
        ObjectStatus.Mismatch => "Mismatch",
        ObjectStatus.MissingInSource => "Missing in Source",
        ObjectStatus.MissingInTarget => "Missing in Target",
        _ => os.ToString()
    };
}

public class EnumDisplayItem
{
    public object Value { get; set; } = null!;
    public string Display { get; set; } = string.Empty;

    public override string ToString() => Display;
}

public static class EnumHelper
{
    public static List<EnumDisplayItem> GetDatabaseTypes() =>
    [
        new() { Value = DatabaseType.SqlServer,  Display = "SQL Server"  },
        new() { Value = DatabaseType.Oracle,     Display = "Oracle"      },
        new() { Value = DatabaseType.PostgreSql, Display = "PostgreSQL"  },
        new() { Value = DatabaseType.MySql,      Display = "MySQL"       },
        new() { Value = DatabaseType.MariaDb,    Display = "MariaDB"     }
    ];

    public static List<EnumDisplayItem> GetEnvironmentTypes() =>
    [
        new() { Value = EnvironmentType.Development, Display = "Development" },
        new() { Value = EnvironmentType.QA,          Display = "QA"          },
        new() { Value = EnvironmentType.UAT,         Display = "UAT"         },
        new() { Value = EnvironmentType.Staging,     Display = "Staging"     },
        new() { Value = EnvironmentType.Production,  Display = "Production"  }
    ];

    public static List<EnumDisplayItem> GetCompareTypes() =>
    [
        new() { Value = CompareType.Database,   Display = "Entire Database"    },
        new() { Value = CompareType.Table,      Display = "Tables"             },
        new() { Value = CompareType.Procedure,  Display = "Stored Procedures"  },
        new() { Value = CompareType.View,       Display = "Views"              },
        new() { Value = CompareType.Function,   Display = "Functions"          },
        new() { Value = CompareType.Trigger,    Display = "Triggers"           }
    ];

    public static List<EnumDisplayItem> GetComparisonScopes() =>
    [
        new() { Value = ComparisonScope.EntireDatabase, Display = "Entire Database" },
        new() { Value = ComparisonScope.SingleObject, Display = "Single Object" },
        new() { Value = ComparisonScope.MultipleObjects, Display = "Multiple Objects" }
    ];

    public static int GetDefaultPort(DatabaseType dbType) => dbType switch
    {
        DatabaseType.SqlServer => 1433,
        DatabaseType.Oracle => 1521,
        DatabaseType.PostgreSql => 5432,
        DatabaseType.MySql => 3306,
        DatabaseType.MariaDb => 3306,
        _ => 0
    };
}
