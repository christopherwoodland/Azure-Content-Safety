namespace NovelCsam.Helpers.Interfaces
{
    public interface IKeyVaultHelper
    {
        public Task<string> GetSecretAsync(string secretName);
    }

}