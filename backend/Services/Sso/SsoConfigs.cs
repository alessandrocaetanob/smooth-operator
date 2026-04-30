using System.Collections.Generic;

namespace Backend.Services.Sso
{
    public class OidcConfig
    {
        public string Authority { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string Scopes { get; set; } = "openid profile email";
        public string SubjectClaim { get; set; } = "sub";
        public string EmailClaim { get; set; } = "email";
        public string NameClaim { get; set; } = "name";
    }

    public class SamlConfig
    {
        public string SpEntityId { get; set; } = string.Empty;
        public string IdpEntityId { get; set; } = string.Empty;
        public string IdpSsoUrl { get; set; } = string.Empty;
        public string IdpCertificate { get; set; } = string.Empty;
        public string SpPrivateKey { get; set; } = string.Empty;
        public string SpCertificate { get; set; } = string.Empty;
        public string NameIdFormat { get; set; } = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress";
        public string AttributeEmail { get; set; } = "email";
        public string AttributeName { get; set; } = "name";
        public bool WantAssertionsSigned { get; set; } = true;
        public bool WantResponseSigned { get; set; } = true;
    }
}
