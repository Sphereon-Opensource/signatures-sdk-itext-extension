# iText extension for the eIDAS Advanced Electronic Signature Client for .NET


This is an extension artifact to support iText as engine to sign PDF's locally so the PDF data (for ie. privacy concerns) does not have leave the local machine.
This document is also an addendum on that artifact. For details about the eIDAS Advanced Electronic Signature Client, please check out the [sphereon-signatures-sdk README](https://www.nuget.org/packages/sphereon-signatures-sdk/).

### APIs
The API object for iText is different than with the regular client, ITextSigningApi; The object takes a configuration provider as a constructor parameter.
````csharp
var apiFactory = new ApiFactory(signaturesSdkConfig, loginResult.IdentityToken, signaturesSdkConfig.ServiceEndpoint);
var configProvider = new ConfigProvider()
                            .WithOnlineCertificates(apiFactory.KeysApi)
                            .WithOnlineConfiguration(apiFactory.SignatureConfigApi)
var iTextSigningApi = new ITextSigningApi(configProvider);
````
The idea is that the ITextSigningApi is called for DetermineSignInput(), Digest() and MergeSignature() while CreateSignature() is still being used on the remote SigningApi. (The cloud or on-premise service has access to the key vault.) 

DetermineSignInput will return a ITextSignInputResponse create the data to sign (SignInput) and a PdfSignerState. The latter contains PDF data itself and the signer state which will need to be kept locally. 
The SignInput part can be passed to the Digest function.
Example:
````csharp
    // Create a signInput object
    var origData = ApiUtils.CreateOrigData(new FileInfo(inputFilePath));
    var determineSignInput = new DetermineSignInput(
        origData: origData,
        signMode: SignMode.DOCUMENT,
        new ConfigKeyBinding(CertificateAlias, signatureConfigId, keyProviderId)
        );
    var signInputResponse = iTextSigningApi.DetermineSignInput(determineSignInput);

    // Create a digest of the input (the signInput object is ammended)
    var createDigest = new Digest(signInputResponse.SignInput);
    var digestResponse = iTextSigningApi.Digest(createDigest);
````
Next the SignInput from the Digest response can be passed to the online CreateSignature function.
````csharp
    // Create a signature
    var createSignature = new CreateSignature(digestResponse.SignInput);
    var signatureResponse = apiFactory.SigningApi.CreateSignature(createSignature);
````
Finally, the signature response is used to create a MergeSignature object which is passed to the MergeSignature function along with the state object that was kept from the DetermineSignInput response.
````csharp
    // Merge the signature onto the PDF document
    var signData = new MergeSignature(
        origData: origData,
        signature: signatureResponse.Signature);
    var signResponse = iTextSigningApi.MergeSignature(signData, signInputResponse.State);

    // Write result PDF to %temp%
    using (var outputStream = File.Create(outputFilePath))
    {
        outputStream.Write(signResponse.SignOutput.Value);
    }
````

### Configuration
The ITextSigningApi needs two types of configuration. The signature definitions and a provider for the certificate chain in X509 format. Both can be dependently provided locally or from the cloud or on-premise, however a key provider configuration has to be created always on the cloud or on-premise service as the remote CreateSignature call needs it. (Referenced by key provider id and certificate alias.) Here is a section from the test code that illustrates this:
````csharp
    // Build & return a ConfigProvider
    switch (mode)
    {
        case Mode.ONLINE:
            {
                CreateConfigOnline(signatureConfig, createKeyProvider);
                return new ConfigProvider()
                    .WithOnlineCertificates(apiFactory.KeysApi)
                    .WithOnlineConfiguration(apiFactory.SignatureConfigApi);
            }

        case Mode.OFFLINE:
            return new ConfigProvider()
                .WithOfflineCertificates(LoadCertificateChain())
                .WithOfflineConfiguration(signatureConfig);
    }
````
It is also possible to make other combinations like 
````csharp
    new ConfigProvider()
        .WithOnlineCertificates(apiFactory.KeysApi)
        .WithOfflineConfiguration(signatureConfig);
````

### Example code
A full working example is available on Github: [signatures-sdk-itext-extension](https://github.com/Sphereon-Opensource/signatures-sdk-itext-extension/tree/main/ITextSignatureTest). The required environment variables are the same as in the [sphereon-signatures-sdk README](https://www.nuget.org/packages/sphereon-signatures-sdk/) 
