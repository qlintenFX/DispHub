// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace DispHub.Core.Schedule;

/// <summary>A city to calculate sunrise and sunset for.</summary>
public sealed record Place(string Name, string Country, double Latitude, double Longitude);

/// <summary>
/// Capitals and large cities, so sunrise and sunset work without location access: DispHub starts from the capital
/// of the Windows region and the user can search for a closer city. Accuracy of a city is plenty: sunrise moves
/// about four minutes per degree of longitude.
/// </summary>
public static class Places
{
    public static IReadOnlyList<Place> All { get; } =
    [
        // Europe
        new("Amsterdam", "NL", 52.37, 4.90), new("Rotterdam", "NL", 51.92, 4.48), new("Groningen", "NL", 53.22, 6.57),
        new("Eindhoven", "NL", 51.44, 5.48), new("Brussels", "BE", 50.85, 4.35), new("Antwerp", "BE", 51.22, 4.40),
        new("Luxembourg", "LU", 49.61, 6.13), new("Paris", "FR", 48.86, 2.35), new("Lyon", "FR", 45.76, 4.84),
        new("Marseille", "FR", 43.30, 5.37), new("Toulouse", "FR", 43.60, 1.44), new("Berlin", "DE", 52.52, 13.40),
        new("Hamburg", "DE", 53.55, 9.99), new("Munich", "DE", 48.14, 11.58), new("Cologne", "DE", 50.94, 6.96),
        new("Frankfurt", "DE", 50.11, 8.68), new("Vienna", "AT", 48.21, 16.37), new("Zurich", "CH", 47.37, 8.54),
        new("Bern", "CH", 46.95, 7.45), new("Geneva", "CH", 46.20, 6.14), new("London", "GB", 51.51, -0.13),
        new("Manchester", "GB", 53.48, -2.24), new("Edinburgh", "GB", 55.95, -3.19), new("Glasgow", "GB", 55.86, -4.25),
        new("Cardiff", "GB", 51.48, -3.18), new("Belfast", "GB", 54.60, -5.93), new("Dublin", "IE", 53.35, -6.26),
        new("Madrid", "ES", 40.42, -3.70), new("Barcelona", "ES", 41.39, 2.17), new("Valencia", "ES", 39.47, -0.38),
        new("Seville", "ES", 37.39, -5.98), new("Lisbon", "PT", 38.72, -9.14), new("Porto", "PT", 41.15, -8.61),
        new("Rome", "IT", 41.90, 12.50), new("Milan", "IT", 45.46, 9.19), new("Naples", "IT", 40.85, 14.27),
        new("Turin", "IT", 45.07, 7.69), new("Copenhagen", "DK", 55.68, 12.57), new("Oslo", "NO", 59.91, 10.75),
        new("Bergen", "NO", 60.39, 5.32), new("Tromsø", "NO", 69.65, 18.96), new("Stockholm", "SE", 59.33, 18.07),
        new("Gothenburg", "SE", 57.71, 11.97), new("Helsinki", "FI", 60.17, 24.94), new("Reykjavik", "IS", 64.15, -21.94),
        new("Warsaw", "PL", 52.23, 21.01), new("Kraków", "PL", 50.06, 19.94), new("Prague", "CZ", 50.08, 14.44),
        new("Bratislava", "SK", 48.15, 17.11), new("Budapest", "HU", 47.50, 19.04), new("Ljubljana", "SI", 46.06, 14.51),
        new("Zagreb", "HR", 45.81, 15.98), new("Belgrade", "RS", 44.79, 20.45), new("Sarajevo", "BA", 43.86, 18.41),
        new("Podgorica", "ME", 42.44, 19.26), new("Skopje", "MK", 42.00, 21.43), new("Tirana", "AL", 41.33, 19.82),
        new("Athens", "GR", 37.98, 23.73), new("Thessaloniki", "GR", 40.64, 22.94), new("Sofia", "BG", 42.70, 23.32),
        new("Bucharest", "RO", 44.43, 26.10), new("Chișinău", "MD", 47.01, 28.86), new("Kyiv", "UA", 50.45, 30.52),
        new("Lviv", "UA", 49.84, 24.03), new("Minsk", "BY", 53.90, 27.56), new("Vilnius", "LT", 54.69, 25.28),
        new("Riga", "LV", 56.95, 24.11), new("Tallinn", "EE", 59.44, 24.75), new("Moscow", "RU", 55.76, 37.62),
        new("Saint Petersburg", "RU", 59.93, 30.36), new("Novosibirsk", "RU", 55.03, 82.92), new("Vladivostok", "RU", 43.12, 131.89),
        new("Istanbul", "TR", 41.01, 28.98), new("Ankara", "TR", 39.93, 32.86), new("Valletta", "MT", 35.90, 14.51),
        new("Nicosia", "CY", 35.17, 33.36), new("Monaco", "MC", 43.74, 7.42), new("Andorra la Vella", "AD", 42.51, 1.52),

        // Americas
        new("Washington", "US", 38.91, -77.04), new("New York", "US", 40.71, -74.01), new("Boston", "US", 42.36, -71.06),
        new("Philadelphia", "US", 39.95, -75.17), new("Atlanta", "US", 33.75, -84.39), new("Miami", "US", 25.76, -80.19),
        new("Chicago", "US", 41.88, -87.63), new("Detroit", "US", 42.33, -83.05), new("Minneapolis", "US", 44.98, -93.27),
        new("Dallas", "US", 32.78, -96.80), new("Houston", "US", 29.76, -95.37), new("Austin", "US", 30.27, -97.74),
        new("Denver", "US", 39.74, -104.99), new("Phoenix", "US", 33.45, -112.07), new("Las Vegas", "US", 36.17, -115.14),
        new("Los Angeles", "US", 34.05, -118.24), new("San Diego", "US", 32.72, -117.16), new("San Francisco", "US", 37.77, -122.42),
        new("Seattle", "US", 47.61, -122.33), new("Portland", "US", 45.52, -122.68), new("Anchorage", "US", 61.22, -149.90),
        new("Honolulu", "US", 21.31, -157.86), new("Ottawa", "CA", 45.42, -75.70), new("Toronto", "CA", 43.65, -79.38),
        new("Montreal", "CA", 45.50, -73.57), new("Vancouver", "CA", 49.28, -123.12), new("Calgary", "CA", 51.05, -114.07),
        new("Winnipeg", "CA", 49.90, -97.14), new("Halifax", "CA", 44.65, -63.58), new("Mexico City", "MX", 19.43, -99.13),
        new("Guadalajara", "MX", 20.66, -103.35), new("Monterrey", "MX", 25.69, -100.32), new("Havana", "CU", 23.11, -82.37),
        new("Santo Domingo", "DO", 18.49, -69.93), new("San Juan", "PR", 18.47, -66.11), new("Guatemala City", "GT", 14.63, -90.51),
        new("San José", "CR", 9.93, -84.08), new("Panama City", "PA", 8.98, -79.52), new("Bogotá", "CO", 4.71, -74.07),
        new("Medellín", "CO", 6.24, -75.58), new("Caracas", "VE", 10.48, -66.90), new("Quito", "EC", -0.18, -78.47),
        new("Lima", "PE", -12.05, -77.04), new("La Paz", "BO", -16.49, -68.12), new("Santiago", "CL", -33.45, -70.67),
        new("Buenos Aires", "AR", -34.60, -58.38), new("Córdoba", "AR", -31.42, -64.18), new("Montevideo", "UY", -34.90, -56.16),
        new("Asunción", "PY", -25.26, -57.58), new("Brasília", "BR", -15.79, -47.88), new("São Paulo", "BR", -23.55, -46.63),
        new("Rio de Janeiro", "BR", -22.91, -43.17), new("Belo Horizonte", "BR", -19.92, -43.94), new("Salvador", "BR", -12.97, -38.50),
        new("Recife", "BR", -8.05, -34.88), new("Porto Alegre", "BR", -30.03, -51.23), new("Manaus", "BR", -3.12, -60.02),
        new("Paramaribo", "SR", 5.85, -55.20), new("Willemstad", "CW", 12.11, -68.93), new("Kingston", "JM", 18.02, -76.80),

        // Asia and the Middle East
        new("Tokyo", "JP", 35.68, 139.69), new("Osaka", "JP", 34.69, 135.50), new("Sapporo", "JP", 43.06, 141.35),
        new("Seoul", "KR", 37.57, 126.98), new("Busan", "KR", 35.18, 129.08), new("Beijing", "CN", 39.90, 116.41),
        new("Shanghai", "CN", 31.23, 121.47), new("Guangzhou", "CN", 23.13, 113.26), new("Shenzhen", "CN", 22.54, 114.06),
        new("Chengdu", "CN", 30.57, 104.07), new("Hong Kong", "HK", 22.32, 114.17), new("Taipei", "TW", 25.03, 121.57),
        new("Manila", "PH", 14.60, 120.98), new("Hanoi", "VN", 21.03, 105.85), new("Ho Chi Minh City", "VN", 10.82, 106.63),
        new("Bangkok", "TH", 13.76, 100.50), new("Kuala Lumpur", "MY", 3.14, 101.69), new("Singapore", "SG", 1.35, 103.82),
        new("Jakarta", "ID", -6.21, 106.85), new("Bali", "ID", -8.65, 115.22), new("Phnom Penh", "KH", 11.56, 104.93),
        new("Yangon", "MM", 16.87, 96.20), new("Dhaka", "BD", 23.81, 90.41), new("Kathmandu", "NP", 27.72, 85.32),
        new("New Delhi", "IN", 28.61, 77.21), new("Mumbai", "IN", 19.08, 72.88), new("Bengaluru", "IN", 12.97, 77.59),
        new("Chennai", "IN", 13.08, 80.27), new("Kolkata", "IN", 22.57, 88.36), new("Hyderabad", "IN", 17.39, 78.49),
        new("Colombo", "LK", 6.93, 79.86), new("Islamabad", "PK", 33.68, 73.05), new("Karachi", "PK", 24.86, 67.01),
        new("Lahore", "PK", 31.55, 74.34), new("Kabul", "AF", 34.56, 69.21), new("Tashkent", "UZ", 41.30, 69.24),
        new("Almaty", "KZ", 43.24, 76.89), new("Astana", "KZ", 51.17, 71.45), new("Ulaanbaatar", "MN", 47.89, 106.91),
        new("Tehran", "IR", 35.69, 51.39), new("Baghdad", "IQ", 33.32, 44.37), new("Riyadh", "SA", 24.71, 46.68),
        new("Jeddah", "SA", 21.49, 39.19), new("Dubai", "AE", 25.20, 55.27), new("Abu Dhabi", "AE", 24.45, 54.38),
        new("Doha", "QA", 25.29, 51.53), new("Kuwait City", "KW", 29.38, 47.99), new("Muscat", "OM", 23.59, 58.41),
        new("Amman", "JO", 31.95, 35.93), new("Beirut", "LB", 33.89, 35.50), new("Jerusalem", "IL", 31.77, 35.21),
        new("Tel Aviv", "IL", 32.09, 34.78), new("Tbilisi", "GE", 41.72, 44.79), new("Yerevan", "AM", 40.18, 44.51),
        new("Baku", "AZ", 40.41, 49.87),

        // Africa
        new("Cairo", "EG", 30.04, 31.24), new("Alexandria", "EG", 31.20, 29.92), new("Casablanca", "MA", 33.57, -7.59),
        new("Rabat", "MA", 34.02, -6.83), new("Algiers", "DZ", 36.75, 3.06), new("Tunis", "TN", 36.81, 10.18),
        new("Tripoli", "LY", 32.89, 13.19), new("Lagos", "NG", 6.52, 3.38), new("Abuja", "NG", 9.08, 7.40),
        new("Accra", "GH", 5.60, -0.19), new("Dakar", "SN", 14.72, -17.47), new("Abidjan", "CI", 5.36, -4.01),
        new("Addis Ababa", "ET", 9.03, 38.74), new("Nairobi", "KE", -1.29, 36.82), new("Kampala", "UG", 0.35, 32.58),
        new("Dar es Salaam", "TZ", -6.79, 39.21), new("Kigali", "RW", -1.95, 30.06), new("Kinshasa", "CD", -4.44, 15.27),
        new("Luanda", "AO", -8.84, 13.23), new("Lusaka", "ZM", -15.39, 28.32), new("Harare", "ZW", -17.83, 31.05),
        new("Maputo", "MZ", -25.97, 32.57), new("Antananarivo", "MG", -18.88, 47.51), new("Johannesburg", "ZA", -26.20, 28.05),
        new("Pretoria", "ZA", -25.75, 28.19), new("Cape Town", "ZA", -33.92, 18.42), new("Durban", "ZA", -29.86, 31.02),
        new("Windhoek", "NA", -22.56, 17.08), new("Gaborone", "BW", -24.65, 25.91), new("Port Louis", "MU", -20.16, 57.50),

        // Oceania
        new("Canberra", "AU", -35.28, 149.13), new("Sydney", "AU", -33.87, 151.21), new("Melbourne", "AU", -37.81, 144.96),
        new("Brisbane", "AU", -27.47, 153.03), new("Perth", "AU", -31.95, 115.86), new("Adelaide", "AU", -34.93, 138.60),
        new("Hobart", "AU", -42.88, 147.33), new("Darwin", "AU", -12.46, 130.84), new("Wellington", "NZ", -41.29, 174.78),
        new("Auckland", "NZ", -36.85, 174.76), new("Christchurch", "NZ", -43.53, 172.64), new("Suva", "FJ", -18.14, 178.44),
        new("Port Moresby", "PG", -9.44, 147.18),
    ];

    /// <summary>The first listed city of a country (its capital or main city), by ISO 3166 code.</summary>
    public static Place? ForCountry(string? isoCode) =>
        string.IsNullOrEmpty(isoCode) ? null : All.FirstOrDefault(p => string.Equals(p.Country, isoCode, StringComparison.OrdinalIgnoreCase));

    /// <summary>Cities whose name starts with, then contains, the text; accents are ignored.</summary>
    public static IReadOnlyList<Place> Search(string text, int max = 8)
    {
        string query = Fold(text.Trim());
        if (query.Length == 0)
            return [];
        return All.Where(p => Fold(p.Name).StartsWith(query, StringComparison.Ordinal))
            .Concat(All.Where(p => Fold(p.Name).Contains(query, StringComparison.Ordinal)))
            .Distinct()
            .Take(max)
            .ToList();
    }

    private static string Fold(string text)
    {
        string decomposed = text.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(c));
        }
        return builder.ToString();
    }
}
