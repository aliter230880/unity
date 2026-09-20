namespace Forma
{
    /// <summary>
    /// Таблицы соответствий «стиль FORMA → CC-контент MAC» (по полу).
    /// Стиль без аналога в каталоге отображается на ближайший (Nearest*);
    /// IsSupported честно сообщает, есть ли прямой аналог.
    /// </summary>
    public static class MacContentCatalog
    {
        // стиль FORMA → путь в Resources (без пола, если общий)
        public static string Hair(string style, bool male) => style switch
        {
            "none" => null,
            "buzz" => "MACHair/Hair_Short_01",
            "short" => "MACHair/Hair_Short_01",
            "pixie" => "MACHair/Hair_Bob_02",
            "sidepart" => male ? "MACHair/Hair_SidePart_01" : "MACHair/Hair_SidePart_02",
            "bob" => "MACHair/Hair_Bob_01",
            "waves" => male ? "MACHair/Hair_Long_01_Masculine" : "MACHair/Hair_Long_01_Feminine",
            "long" => male ? "MACHair/Hair_Long_01_Masculine" : "MACHair/Hair_Long_01_Feminine",
            "ponytail" => "MACHair/Hair_Ponytail_01",
            "bun" => "MACHair/Hair_Ponytail_01",       // прямой аналогии нет — ближайший
            "afro" => "MACHair/Hair_Afro_Short_01",
            "curls" => "MACHair/Hair_Afro_Curl_01",
            _ => null,
        };

        public static string Upper(string id, bool male) => id switch
        {
            "none" => null,
            "bandeau" => male ? "MACClothing/Tank_Top_01" : "MACClothing/Bra",
            "tank" => "MACClothing/Tank_Top_01",
            "tee" => male ? "MACClothing/TShirt_01_M" : "MACClothing/TShirt_01_F",
            "crop" => male ? "MACClothing/TShirt_01_M" : "MACClothing/Crop_Top_01",
            "shirt" => male ? "MACClothing/Shirt_01" : "MACClothing/Shirt_01_F",
            _ => null,
        };

        public static string Lower(string id, bool male) => id switch
        {
            "none" => null,
            "briefs" => male ? "MACClothing/Underwear_M" : "MACClothing/Underwear_F",
            "shorts" => "MACClothing/Shorts_01",
            "pants" => male ? "MACClothing/Jeans_01_M_Blue" : "MACClothing/Jeans_01_F_Blue",
            "skirt" => male ? "MACClothing/Jeans_01_M_Blue" : "MACClothing/Jeans_01_F_Blue", // юбок в CC-каталоге нет
            "leggings" => "MACClothing/Jogging_Pants",
            _ => null,
        };

        /// <summary>Есть ли прямой аналог в контенте MAC (для честного UI: bun/skirt — подставки).</summary>
        public static bool HairExact(string style) => style is not ("bun" or null or "");
        public static bool UpperExact(string id) => true;
        public static bool LowerExact(string id, bool male) => id != "skirt" || !male; // юбка есть только условно
    }
}
