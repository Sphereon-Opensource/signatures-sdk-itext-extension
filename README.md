# iText extension for the eIDAS Advanced Electronic Signature Client for .NET


This is an extension artifact to support iText as engine to sign PDF's locally so the PDF data (for ie. privacy concerns) does not have leave the local machine.
This document is also an addendum on that artifact.  
For details about the eIDAS Advanced Electronic Signature Client, please check out the [sphereon-signatures-sdk README on NuGet](https://www.nuget.org/packages/sphereon-signatures-sdk/) first.  
The full API spec is in our [eidas-signature-openapi GitHub repository](https://github.com/Sphereon-Opensource/eidas-signature-openapi).  
The definition of the underlaying API of this SDK can be [viewed here](https://eidas-signature-ms.icywave-74e8c4b0.westeurope.azurecontainerapps.io/swagger-ui.html) in a ui (swagger.) 



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
#### Configuration provider
The ITextSigningApi needs two configurations. The signature definitions and a provider for the certificate chain in X509 format. Both can be dependently provided locally or from the cloud or on-premise, however a key provider configuration has to be created always on the cloud or on-premise service as the remote CreateSignature call requires it (referenced by key provider id and certificate alias.) Here is a sample from the test code that demonstrates this:
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

#### Signature configuration
The Signature Configuration is described in the [sphereon-signatures-sdk README](https://www.nuget.org/packages/sphereon-signatures-sdk/) under §"Signature Configuration" and is part of test code file [AbstractTestBase.cs](https://github.com/Sphereon-Opensource/signatures-sdk-itext-extension/blob/main/ITextSignatureTest/AbstractTestBase.cs)
The latter also contains sample code of a visual signature.

#### Key Provider configuration
The Signature Configuration is described in the [sphereon-signatures-sdk README](https://www.nuget.org/packages/sphereon-signatures-sdk/) under §"Key Provider Service" and is also part of [AbstractTestBase.cs](https://github.com/Sphereon-Opensource/signatures-sdk-itext-extension/blob/main/ITextSignatureTest/AbstractTestBase.cs)



#### LTV
The testcode of this project is configured to produce LTV enabled signatures. To enable LTV, field "signatureLevel" in SignatureConfig has to be set to an "_LT" or "_LTA" variant, i.e. PAdESBASELINELT or PKCS7LT.


### Example code
A full working example is available in this repository in directory ITextSignatureTest. (GitHub liink: [signatures-sdk-itext-extension tests](https://github.com/Sphereon-Opensource/signatures-sdk-itext-extension/tree/main/ITextSignatureTest).)  
The required environment variables are the same as in the [sphereon-signatures-sdk README](https://www.nuget.org/packages/sphereon-signatures-sdk/) and the output files are written to %TEMP%.


### License / copyrights
This library incorporates code from iText Group NV, copyright (c). All rights reserved. The use of this software is subject to the terms of use available at http://itextpdf.com/terms-of-use/.  
As a result, this library is also licensed under the AGPLv3 license. To use this library for commercial purposes, the user must purchase a separate iText license.
Once the iText license requirements are fulfilled, usage of this library is automatically granted.
