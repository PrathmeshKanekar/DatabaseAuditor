namespace DatabaseAuditor.WPF.Converters;

using System;
using System.Globalization;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

public class PageNameToViewConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string pageName) return null;

        Log.Information("Navigation requested: {PageName}", pageName);

        try
        {
            Type? viewType = pageName switch
            {
                "Dashboard" => typeof(Views.DashboardView),
                "Connections" => typeof(Views.ConnectionsView),
                "Compare" => typeof(Views.CompareView),
                "Results" => typeof(Views.ResultsView),
                "Reports" => typeof(Views.ReportsView),
                "Settings" => typeof(Views.SettingsView),
                _ => null
            };

            if (viewType == null)
            {
                Log.Warning("No view type mapped for page name: {PageName}", pageName);
                return null;
            }

            Log.Information("Resolving view: {ViewType}", viewType.Name);
            var view = App.ServiceProvider.GetRequiredService(viewType);
            Log.Information("Navigation successful");
            return view;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Navigation failure");
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
