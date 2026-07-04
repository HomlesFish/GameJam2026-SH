namespace GameJam2026
{
    /// <summary>
    /// 声呐频段。三个频段的物体在雷达上同时显示（靠各自贴图/颜色区分）；
    /// 切换频段只改变"听声"播放的音频。
    /// </summary>
    public enum SonarBand
    {
        Low = 0,   // 1 Hz ~ 1 kHz
        Mid = 1,   // 1 kHz ~ 2 kHz
        High = 2   // 2 kHz ~ 200 kHz
    }

    /// <summary>频段名称/范围文本，供 HUD 显示。</summary>
    public static class SonarBandInfo
    {
        public static string GetName(SonarBand band)
        {
            switch (band)
            {
                case SonarBand.Low: return "低频";
                case SonarBand.Mid: return "中频";
                case SonarBand.High: return "高频";
                default: return band.ToString();
            }
        }

        public static string GetRange(SonarBand band)
        {
            switch (band)
            {
                case SonarBand.Low: return "1 Hz ~ 1 kHz";
                case SonarBand.Mid: return "1 kHz ~ 2 kHz";
                case SonarBand.High: return "2 kHz ~ 200 kHz";
                default: return string.Empty;
            }
        }

        /// <summary>HUD 用：如 "中频 · 1 kHz ~ 2 kHz"。</summary>
        public static string GetDisplay(SonarBand band) => GetName(band) + " · " + GetRange(band);
    }
}
