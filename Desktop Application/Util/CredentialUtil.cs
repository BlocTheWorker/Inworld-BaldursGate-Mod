using CredentialManagement;

namespace BaldursGateInworld.Util
{
    internal struct InworldCredential
    {
        internal string Key;
        internal string Secret;
    }

    internal static class CredentialUtil
    {
        internal static readonly string StaticTarget = "InworldBaldursGate";
        internal static bool SetCredentials(string key, string secret)
        {
            string combined = key + "##" + secret;
            return new Credential
            {
                Target = StaticTarget,
                Username = StaticTarget,
                Password = combined,
                PersistanceType = PersistanceType.LocalComputer
            }.Save();
        }

        internal static InworldCredential GetCredential()
        {
            var credential = new Credential { Target = StaticTarget };
            if (!credential.Load())
            {
                return new InworldCredential() { };
            }

            var pair = credential.Password.Split("##");
            return new InworldCredential() { Key = pair[0], Secret = pair[1] };
        }
    }

}
