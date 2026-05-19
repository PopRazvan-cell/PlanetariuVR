using System;
using UnityEngine;

/// <summary>
/// Clasă utilitară statică pentru calcule de astronomie pozițională.
/// Conține formulele necesare pentru a converti coordonatele ecuatoriale (fixe pe bolta cerească)
/// în coordonate orizontale (raportate la observatorul de pe Pământ).
/// </summary>
public static class AstroMath
{
    /// <summary>
    /// Calculează Zilele Iuliene (Julian Date - JD) pornind de la o dată calendaristică standard (UTC).
    /// Julian Date reprezintă numărul continuu de zile trecute de la 1 Ianuarie 4713 î.Hr.
    /// Este standardul absolut de timp folosit în astronomie pentru a evita problemele anilor bisecți.
    /// </summary>
    public static double CalculateJulianDate(DateTime date)
    {
        int year = date.Year;
        int month = date.Month;
        int day = date.Day;

        // Dacă luna este ianuarie sau februarie, o considerăm luna a 13-a sau a 14-a a anului precedent
        // (conform algoritmului astronomic standard)
        if (month <= 2)
        {
            year -= 1;
            month += 12;
        }

        double a = Mathf.Floor(year / 100f);
        double b = 2 - a + Mathf.Floor((float)(a / 4));
        
        // Fracțiunea din zi (ora exactă convertită în zecimale)
        double timeFraction = (date.Hour + (date.Minute / 60f) + (date.Second / 3600f)) / 24f;

        return Mathf.Floor(365.25f * (year + 4716)) + Mathf.Floor(30.6001f * (month + 1)) + day + timeFraction + b - 1524.5;
    }

    /// <summary>
    /// Calculează Timpul Sideral Local (Local Sidereal Time - LST) în grade.
    /// LST indică "ora stelelor": ce coordonată de Ascensie Dreaptă se află exact pe meridianul local.
    /// Pământul se rotește cu aprox 1 grad la fiecare 4 minute, deci LST-ul se schimbă constant.
    /// </summary>
    public static double CalculateLocalSiderealTime(double julianDate, float longitude)
    {
        // 2451545.0 reprezintă Epoca J2000.0 (1 Ianuarie 2000 la prânz).
        // Calculăm zilele scurse de la acel punct de referință.
        double daysSinceJ2000 = julianDate - 2451545.0; 
        
        // Calculăm Timpul Sideral la Meridianul Greenwich (GMST) folosind constanta rotației Pământului
        double gmst = 280.46061837 + 360.98564736629 * daysSinceJ2000;
        
        // Ajustăm pentru longitudinea locală a observatorului și păstrăm valoarea în intervalul 0-360 grade
        double lst = (gmst + longitude) % 360.0;
        if (lst < 0) lst += 360.0;

        return lst;
    }

    /// <summary>
    /// Aplică Trigonometria Sferică pentru a transforma coordonatele ecuatoriale (RA, Dec) 
    /// în coordonate orizontale (Altitudine, Azimut).
    /// </summary>
    /// <param name="ra">Ascensia Dreaptă (Right Ascension) în grade.</param>
    /// <param name="dec">Declinația în grade.</param>
    /// <param name="latitude">Latitudinea observatorului în grade.</param>
    /// <param name="lst">Timpul Sideral Local în grade.</param>
    /// <param name="altitude">Ieșire: Cât de sus este steaua față de orizont (în radiani).</param>
    /// <param name="azimuth">Ieșire: Direcția cardinală a stelei (în radiani).</param>
    public static void EquatorialToHorizontal(float ra, float dec, float latitude, double lst, out float altitude, out float azimuth)
    {
        // Unghiul Orar (Hour Angle) = distanța unghiulară dintre stea și meridianul local
        double ha = lst - ra;

        // Conversie din grade în radiani (necesar pentru funcțiile trigonometrice Mathf)
        float latRad = latitude * Mathf.Deg2Rad;
        float decRad = dec * Mathf.Deg2Rad;
        float haRad = (float)ha * Mathf.Deg2Rad;

        // 1. Calculăm Altitudinea (sinusul altitudinii)
        float sinAlt = Mathf.Sin(decRad) * Mathf.Sin(latRad) + Mathf.Cos(decRad) * Mathf.Cos(latRad) * Mathf.Cos(haRad);
        altitude = Mathf.Asin(Mathf.Clamp(sinAlt, -1f, 1f));
        
        // 2. Calculăm Azimutul (cosinusul azimutului)
        float cosAz = (Mathf.Sin(decRad) - Mathf.Sin(altitude) * Mathf.Sin(latRad)) / (Mathf.Cos(altitude) * Mathf.Cos(latRad));
        azimuth = Mathf.Acos(Mathf.Clamp(cosAz, -1f, 1f));
        
        // Corectăm azimutul: dacă unghiul orar e pozitiv, steaua a trecut de meridian (e în Vest)
        if (Mathf.Sin(haRad) > 0)
        {
            azimuth = (2 * Mathf.PI) - azimuth;
        }
    }

    /// <summary>
    /// Convertește coordonatele sferice (Alt, Az, Rază) în coordonate carteziene 3D (X, Y, Z) specifice sistemului Unity.
    /// În Unity: axa Y este sus (Altitudine), planul XZ reprezintă Pământul plat.
    /// </summary>
    public static Vector3 SphericalToCartesian(float altRad, float azRad, float radius)
    {
        float x = radius * Mathf.Cos(altRad) * Mathf.Sin(azRad);
        float y = radius * Mathf.Sin(altRad); // Y este altitudinea pe cer
        float z = radius * Mathf.Cos(altRad) * Mathf.Cos(azRad);

        return new Vector3(x, y, z);
    }
}