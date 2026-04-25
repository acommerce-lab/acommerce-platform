namespace Ejar.Admin.Web.Store;

public interface ITranslationProvider
{
    string Translate(string key, string language);
}
