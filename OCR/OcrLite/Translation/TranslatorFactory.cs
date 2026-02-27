namespace OcrLite.Translation;

internal static class TranslatorFactory
{
    public static bool TryCreate(TranslationSettings settings, out ITextTranslator translator, out string error)
    {
        error = string.Empty;

        switch (settings.Type)
        {
            case TranslatorType.None:
                translator = new IdentityTranslator();
                return true;

            case TranslatorType.DeepL:
                if (string.IsNullOrWhiteSpace(settings.DeepLApiKey))
                {
                    translator = new IdentityTranslator();
                    error = "DeepL API key is required.";
                    return false;
                }

                translator = new DeepLTranslator(settings);
                return true;

            case TranslatorType.Google:
                bool hasApiKey = !string.IsNullOrWhiteSpace(settings.GoogleApiKey);
                bool hasManualOAuth =
                    !string.IsNullOrWhiteSpace(settings.GoogleAccessToken) &&
                    !string.IsNullOrWhiteSpace(settings.GoogleProjectId);
                bool hasRefreshOAuth =
                    !string.IsNullOrWhiteSpace(settings.GoogleClientId) &&
                    !string.IsNullOrWhiteSpace(settings.GoogleClientSecret) &&
                    !string.IsNullOrWhiteSpace(settings.GoogleRefreshToken) &&
                    !string.IsNullOrWhiteSpace(settings.GoogleProjectId);

                if (!hasApiKey && !hasManualOAuth && !hasRefreshOAuth)
                {
                    translator = new IdentityTranslator();
                    error = "Google API key or OAuth(access token + project id) or OAuth(refresh token flow) is required.";
                    return false;
                }

                translator = new GoogleTranslator(settings);
                return true;

            case TranslatorType.Papago:
                if (string.IsNullOrWhiteSpace(settings.PapagoClientId) || string.IsNullOrWhiteSpace(settings.PapagoClientSecret))
                {
                    translator = new IdentityTranslator();
                    error = "Papago client id and secret are required.";
                    return false;
                }

                translator = new PapagoTranslator(settings);
                return true;

            default:
                translator = new IdentityTranslator();
                error = "Unknown translator type.";
                return false;
        }
    }
}
