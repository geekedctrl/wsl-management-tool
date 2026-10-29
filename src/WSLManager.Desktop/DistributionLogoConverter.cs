using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace WSLManager_Desktop;

public sealed class DistributionLogoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var key = value as string;
        if (string.IsNullOrWhiteSpace(key)) key = "linux";

        var assetPath = Path.Combine(AppContext.BaseDirectory, "Assets", "DistributionLogos", $"{key}.svg");
        if (!File.Exists(assetPath))
            assetPath = Path.Combine(AppContext.BaseDirectory, "Assets", "DistributionLogos", "linux.svg");

        return new SvgImageSource { UriSource = new Uri(assetPath) };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
