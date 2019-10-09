﻿//------------------------------------------------------------------------------
//
// Copyright (c) Microsoft Corporation.
// All rights reserved.
//
// This code is licensed under the MIT License.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files(the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions :
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Json;
using Microsoft.IdentityModel.Json.Linq;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.SignedHttpRequest;
using Microsoft.IdentityModel.TestUtils;
using Microsoft.IdentityModel.Tokens;
using Xunit;

#pragma warning disable CS3016 // Arrays as attribute arguments is not CLS-compliant

namespace Microsoft.IdentityModel.Protocols.SignedHttpRequest.Tests
{
    public class SignedHttpRequestCreationTests
    {
        [Fact]
        public async Task CreateSignedHttpRequest()
        {
            var context = TestUtilities.WriteHeader($"{this}.CreateSignedHttpRequest", "", true);

            var handler = new SignedHttpRequestHandlerPublic();
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await handler.CreateSignedHttpRequestAsync(null, CancellationToken.None).ConfigureAwait(false));

            var signedHttpRequestDescriptor = new SignedHttpRequestDescriptor(SignedHttpRequestTestUtils.DefaultEncodedAccessToken, new HttpRequestData(), SignedHttpRequestTestUtils.DefaultSigningCredentials, new SignedHttpRequestCreationParameters() { CreateM = false, CreateP = false, CreateU = false });
            var signedHttpRequestString = await handler.CreateSignedHttpRequestAsync(signedHttpRequestDescriptor, CancellationToken.None).ConfigureAwait(false);


            var tvp = new TokenValidationParameters()
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = false,
                ValidateLifetime = false,
                IssuerSigningKey = SignedHttpRequestTestUtils.DefaultSigningCredentials.Key
            };
            var result = new JsonWebTokenHandler().ValidateToken(signedHttpRequestString, tvp);

            if (result.IsValid == false)
                context.AddDiff($"Not able to create and validate signed http request token");

            TestUtilities.AssertFailIfErrors(context);
        }

        [Theory, MemberData(nameof(CreateHeaderTheoryData))]
        public void CreateHeader(CreateSignedHttpRequestTheoryData theoryData)
        {
            var context = TestUtilities.WriteHeader($"{this}.CreateHeader", theoryData);
            try
            {
                var handler = new SignedHttpRequestHandlerPublic();
                var signedHttpRequestDescriptor = theoryData.BuildSignedHttpRequestDescriptor();

                var headerString = handler.CreateHttpRequestHeaderPublic(signedHttpRequestDescriptor);
                var header = JObject.Parse(headerString);

                if (!header.ContainsKey(theoryData.ExpectedClaim))
                    context.AddDiff($"Header doesn't contain the claim '{theoryData.ExpectedClaim}'");

                if (!IdentityComparer.AreStringsEqual(header.Value<string>(theoryData.ExpectedClaim), theoryData.ExpectedClaimValue, context))
                    context.AddDiff($"Value of '{theoryData.ExpectedClaim}' claim is '{header.Value<string>(theoryData.ExpectedClaim)}', but expected value was '{theoryData.ExpectedClaimValue}'");

                theoryData.ExpectedException.ProcessNoException(context);
            }
            catch (Exception ex)
            {
                theoryData.ExpectedException.ProcessException(ex, context);
            }

            TestUtilities.AssertFailIfErrors(context);
        }

        public static TheoryData<CreateSignedHttpRequestTheoryData> CreateHeaderTheoryData
        {
            get
            {
                return new TheoryData<CreateSignedHttpRequestTheoryData>
                {
                    new CreateSignedHttpRequestTheoryData
                    {
                        First = true,
                        ExpectedClaim = JwtHeaderParameterNames.Typ,
                        ExpectedClaimValue = SignedHttpRequestConstants.TokenType,
                        TestId = "ExpectedTokenType",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = JwtHeaderParameterNames.Kid,
                        ExpectedClaimValue =  SignedHttpRequestTestUtils.DefaultSigningCredentials.Kid,
                        SigningCredentials = SignedHttpRequestTestUtils.DefaultSigningCredentials,
                        TestId = "ExpectedKid",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = JwtHeaderParameterNames.X5t,
                        ExpectedClaimValue =  ((X509SecurityKey)Default.AsymmetricSigningCredentials.Key).X5t,
                        SigningCredentials = Default.AsymmetricSigningCredentials,
                        TestId = "ExpectedX5t",
                    },
                };
            }
        }

        [Theory, MemberData(nameof(SignHttpRequestTheoryData))]
        public async void SignHttpRequest(CreateSignedHttpRequestTheoryData theoryData)
        {
            var context = TestUtilities.WriteHeader($"{this}.SignHttpRequest", theoryData);
            try
            {
                var handler = new SignedHttpRequestHandlerPublic();
                var signedHttpRequestDescriptor = theoryData.BuildSignedHttpRequestDescriptor();
                var signedHttpRequestString = await handler.SignHttpRequestPublicAsync(theoryData.HeaderString, theoryData.PayloadString, signedHttpRequestDescriptor, CancellationToken.None).ConfigureAwait(false);

                var tvp = new TokenValidationParameters()
                {
                    ValidateAudience = false,
                    ValidateIssuer = false,
                    ValidateIssuerSigningKey = false,
                    ValidateLifetime = false,
                    IssuerSigningKey = signedHttpRequestDescriptor.SigningCredentials.Key
                };
                var result = new JsonWebTokenHandler().ValidateToken(signedHttpRequestString, tvp);

                if (result.IsValid == false)
                    context.AddDiff($"Not able to create and validate signed http request token");

                theoryData.ExpectedException.ProcessNoException(context);
            }
            catch (Exception ex)
            {
                theoryData.ExpectedException.ProcessException(ex, context);
            }

            TestUtilities.AssertFailIfErrors(context);
        }

        public static TheoryData<CreateSignedHttpRequestTheoryData> SignHttpRequestTheoryData
        {
            get
            {
                return new TheoryData<CreateSignedHttpRequestTheoryData>
                {
                    new CreateSignedHttpRequestTheoryData
                    {
                        First = true,
                        HeaderString = null,
                        ExpectedException = ExpectedException.ArgumentNullException(),
                        TestId = "HeaderStringNull",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        HeaderString = "",
                        ExpectedException = ExpectedException.ArgumentNullException(),
                        TestId = "HeaderStringEmpty",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        HeaderString = "dummyData",
                        PayloadString = null,
                        ExpectedException = ExpectedException.ArgumentNullException(),
                        TestId = "PayloadStringNull",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        HeaderString = "dummyData",
                        PayloadString = "",
                        ExpectedException = ExpectedException.ArgumentNullException(),
                        TestId = "PayloadStringEmpty",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        HeaderString = "{\"alg\": \"http://www.w3.org/2001/04/xmldsig-more#rsa-sha256\"}",
                        PayloadString = "{\"claim\": 1}",
                        SigningCredentials =  SignedHttpRequestTestUtils.DefaultSigningCredentials,
                        TestId = "ValidSignedHttpRequest",
                    },
                };
            }
        }

        [Theory, MemberData(nameof(CreateClaimCallsTheoryData))]
        public void CreateClaimCalls(CreateSignedHttpRequestTheoryData theoryData)
        {
            var context = TestUtilities.WriteHeader($"{this}.CreateClaimCalls", theoryData);
            try
            {
                var handler = new SignedHttpRequestHandlerPublic();
                var signedHttpRequestDescriptor = theoryData.BuildSignedHttpRequestDescriptor();

                var payloadString = handler.CreateHttpRequestPayloadPublic(signedHttpRequestDescriptor);
                var payload = JObject.Parse(payloadString);

                foreach (var payloadItem in payload)
                {
                    if (!theoryData.ExpectedPayloadClaims.Contains(payloadItem.Key))
                        context.AddDiff($"ExpectedPayloadClaims doesn't contain the claim '{payloadItem.Key}'");
                }

                foreach (var expectedClaim in theoryData.ExpectedPayloadClaims)
                {
                    if (!payload.ContainsKey(expectedClaim))
                        context.AddDiff($"Payload doesn't contain the claim '{expectedClaim}'");
                }

                theoryData.ExpectedException.ProcessNoException(context);
            }
            catch (Exception ex)
            {
                theoryData.ExpectedException.ProcessException(ex, context);
            }

            TestUtilities.AssertFailIfErrors(context);
        }

        public static TheoryData<CreateSignedHttpRequestTheoryData> CreateClaimCallsTheoryData
        {
            get
            {
                return new TheoryData<CreateSignedHttpRequestTheoryData>
                {
                    new CreateSignedHttpRequestTheoryData
                    {
                        First = true,
                        ExpectedPayloadClaims = new List<string>() { "at" },
                        SignedHttpRequestCreationParameters = new SignedHttpRequestCreationParameters()
                        {
                            CreateB = false,
                            CreateH = false,
                            CreateM = false,
                            CreateNonce = false,
                            CreateP = false,
                            CreateQ = false,
                            CreateTs = false,
                            CreateU = false,
                            CustomNonceCreator = null,
                            AdditionalClaimCreator = null
                        },
                        TestId = "NoClaimsCreated",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedPayloadClaims = new List<string>() { "at", "b", "h", "m", "nonce", "p", "q", "ts", "u", "additionalClaim" },
                        SignedHttpRequestCreationParameters = new SignedHttpRequestCreationParameters()
                        {
                            CreateB = true,
                            CreateH = true,
                            CreateM = true,
                            CreateNonce = true,
                            CreateP = true,
                            CreateQ = true,
                            CreateTs = true,
                            CreateU = true,
                            CustomNonceCreator = null,
                            AdditionalClaimCreator = (IDictionary<string, object> payload, SignedHttpRequestDescriptor signedHttpRequestDescriptor) => payload.Add("additionalClaim", "additionalClaimValue"),
                        },
                        HttpRequestBody = Guid.NewGuid().ToByteArray(),
                        HttpRequestHeaders = new Dictionary<string, IEnumerable<string>>()
                        {
                            { "header1", new List<string>() {"headerValue1"} }
                        },
                        HttpRequestMethod = "GET",
                        HttpRequestUri = new Uri("https://www.contoso.com/path1?queryParam1=quertValue1"),
                        TestId = "AllClaimsCreated",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        Payload = null,
                        ExpectedException = ExpectedException.ArgumentNullException(),
                        TestId = "NullPayload",
                    },
                };
            }
        }

        [Theory, MemberData(nameof(CreateAtClaimTheoryData))]
        public void CreateAtClaim(CreateSignedHttpRequestTheoryData theoryData)
        {
            var context = TestUtilities.WriteHeader($"{this}.CreateAtClaimTheoryData", theoryData);
            try
            {
                var handler = new SignedHttpRequestHandlerPublic();
                var signedHttpRequestDescriptor = theoryData.BuildSignedHttpRequestDescriptor();

                handler.AddAtClaimPublic(theoryData.Payload, signedHttpRequestDescriptor);
                var payload = JObject.Parse(handler.ConvertToJsonPublic(theoryData.Payload));

                if (!payload.ContainsKey(theoryData.ExpectedClaim))
                    context.AddDiff($"Payload doesn't contain the claim '{theoryData.ExpectedClaim}'");

                if (!IdentityComparer.AreStringsEqual(payload.Value<string>(theoryData.ExpectedClaim), theoryData.ExpectedClaimValue, context))
                    context.AddDiff($"Value of '{theoryData.ExpectedClaim}' claim is '{payload.Value<string>(theoryData.ExpectedClaim)}', but expected value was '{theoryData.ExpectedClaimValue}'");

                theoryData.ExpectedException.ProcessNoException(context);
            }
            catch (Exception ex)
            {
                theoryData.ExpectedException.ProcessException(ex, context);
            }

            TestUtilities.AssertFailIfErrors(context);
        }

        public static TheoryData<CreateSignedHttpRequestTheoryData> CreateAtClaimTheoryData
        {
            get
            {
                return new TheoryData<CreateSignedHttpRequestTheoryData>
                {
                    new CreateSignedHttpRequestTheoryData
                    {
                        First = true,
                        ExpectedClaim = SignedHttpRequestClaimTypes.At,
                        ExpectedClaimValue = SignedHttpRequestTestUtils.DefaultEncodedAccessToken,
                        TestId = "ValidAt",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        Payload = null,
                        ExpectedException = ExpectedException.ArgumentNullException(),
                        TestId = "NullPayload",
                    },
                };
            }
        }

        [Theory, MemberData(nameof(CreateTsClaimTheoryData))]
        public void CreateTsClaim(CreateSignedHttpRequestTheoryData theoryData)
        {
            var context = TestUtilities.WriteHeader($"{this}.CreateTsClaim", theoryData);
            try
            {
                var handler = new SignedHttpRequestHandlerPublic();
                var signedHttpRequestDescriptor = theoryData.BuildSignedHttpRequestDescriptor();

                handler.AddTsClaimPublic(theoryData.Payload, signedHttpRequestDescriptor);
                var payload = JObject.Parse(handler.ConvertToJsonPublic(theoryData.Payload));

                if (!payload.ContainsKey(theoryData.ExpectedClaim))
                    context.AddDiff($"Payload doesn't contain the claim '{theoryData.ExpectedClaim}'");

                if (!IdentityComparer.AreEqual(payload.Value<long>(theoryData.ExpectedClaim), theoryData.ExpectedClaimValue))
                    context.AddDiff($"Value of '{theoryData.ExpectedClaim}' claim is '{payload.Value<long>(theoryData.ExpectedClaim)}', but expected value was '{theoryData.ExpectedClaimValue}'");

                theoryData.ExpectedException.ProcessNoException(context);
            }
            catch (Exception ex)
            {
                theoryData.ExpectedException.ProcessException(ex, context);
            }

            TestUtilities.AssertFailIfErrors(context);
        }

        public static TheoryData<CreateSignedHttpRequestTheoryData> CreateTsClaimTheoryData
        {
            get
            {
                var timeNow = new DateTime(2019, 01, 01, 01, 01, 01, 01);
                return new TheoryData<CreateSignedHttpRequestTheoryData>
                {
                    new CreateSignedHttpRequestTheoryData
                    {
                        First = true,
                        CallContext = new CallContext() { PropertyBag = new Dictionary<string, object>() { {"MockAddTsClaim", timeNow } } },
                        ExpectedClaim = SignedHttpRequestClaimTypes.Ts,
                        ExpectedClaimValue = (long)(timeNow - EpochTime.UnixEpoch).TotalSeconds,
                        TestId = "ValidTs",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        CallContext = new CallContext() { PropertyBag = new Dictionary<string, object>() { {"MockAddTsClaim", timeNow } } },
                        SignedHttpRequestCreationParameters = new SignedHttpRequestCreationParameters() { TimeAdjustment = TimeSpan.FromMinutes(-1) },
                        ExpectedClaim = SignedHttpRequestClaimTypes.Ts,
                        ExpectedClaimValue = (long)(timeNow - EpochTime.UnixEpoch).TotalSeconds - 60,
                        TestId = "ValidTsWithTimeAdjustmentMinus",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        CallContext = new CallContext() { PropertyBag = new Dictionary<string, object>() { {"MockAddTsClaim", timeNow } } },
                        SignedHttpRequestCreationParameters = new SignedHttpRequestCreationParameters() { TimeAdjustment = TimeSpan.FromMinutes(1) },
                        ExpectedClaim = SignedHttpRequestClaimTypes.Ts,
                        ExpectedClaimValue = (long)(timeNow - EpochTime.UnixEpoch).TotalSeconds + 60,
                        TestId = "ValidTsWithTimeAdjustmentPlus",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        Payload = null,
                        ExpectedException = ExpectedException.ArgumentNullException(),
                        TestId = "NullPayload",
                    },
                };
            }
        }

        [Theory, MemberData(nameof(CreateMClaimTheoryData))]
        public void CreateMClaim(CreateSignedHttpRequestTheoryData theoryData)
        {
            var context = TestUtilities.WriteHeader($"{this}.CreateMClaim", theoryData);
            try
            {
                var handler = new SignedHttpRequestHandlerPublic();
                var signedHttpRequestDescriptor = theoryData.BuildSignedHttpRequestDescriptor();

                handler.AddMClaimPublic(theoryData.Payload, signedHttpRequestDescriptor);
                var payload = JObject.Parse(handler.ConvertToJsonPublic(theoryData.Payload));

                if (!payload.ContainsKey(theoryData.ExpectedClaim))
                    context.AddDiff($"Payload doesn't contain the claim '{theoryData.ExpectedClaim}'");

                if (!IdentityComparer.AreStringsEqual(payload.Value<string>(theoryData.ExpectedClaim), theoryData.ExpectedClaimValue, context))
                    context.AddDiff($"Value of '{theoryData.ExpectedClaim}' claim is '{payload.Value<string>(theoryData.ExpectedClaim)}', but expected value was '{theoryData.ExpectedClaimValue}'");

                theoryData.ExpectedException.ProcessNoException(context);
            }
            catch (Exception ex)
            {
                theoryData.ExpectedException.ProcessException(ex, context);
            }

            TestUtilities.AssertFailIfErrors(context);
        }

        public static TheoryData<CreateSignedHttpRequestTheoryData> CreateMClaimTheoryData
        {
            get
            {
                return new TheoryData<CreateSignedHttpRequestTheoryData>
                {
                    new CreateSignedHttpRequestTheoryData
                    {
                        First = true,
                        ExpectedClaim = SignedHttpRequestClaimTypes.M,
                        ExpectedClaimValue = "GET",
                        HttpRequestMethod = "GET",
                        TestId = "ValidM",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaimValue = "GET",
                        HttpRequestMethod = "get",
                        ExpectedException = new ExpectedException(typeof(SignedHttpRequestCreationException), "IDX23002"),
                        TestId = "InvalidLowercaseM",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        Payload = null,
                        ExpectedException = ExpectedException.ArgumentNullException(),
                        TestId = "NullPayload",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        HttpRequestMethod = "",
                        ExpectedException = ExpectedException.ArgumentNullException(),
                        TestId = "EmptyM",
                    },
                };
            }
        }

        [Theory, MemberData(nameof(CreateUClaimTheoryData))]
        public void CreateUClaim(CreateSignedHttpRequestTheoryData theoryData)
        {
            var context = TestUtilities.WriteHeader($"{this}.CreateUClaim", theoryData);
            try
            {
                var handler = new SignedHttpRequestHandlerPublic();
                var signedHttpRequestDescriptor = theoryData.BuildSignedHttpRequestDescriptor();

                handler.AddUClaimPublic(theoryData.Payload, signedHttpRequestDescriptor);
                var payload = JObject.Parse(handler.ConvertToJsonPublic(theoryData.Payload));

                if (!theoryData.Payload.ContainsKey(theoryData.ExpectedClaim))
                    context.AddDiff($"Payload doesn't contain the claim '{theoryData.ExpectedClaim}'");

                if (!IdentityComparer.AreStringsEqual(payload.Value<string>(theoryData.ExpectedClaim), theoryData.ExpectedClaimValue, context))
                    context.AddDiff($"Value of '{theoryData.ExpectedClaim}' claim is '{payload.Value<string>(theoryData.ExpectedClaim)}', but expected value was '{theoryData.ExpectedClaimValue}'");

                theoryData.ExpectedException.ProcessNoException(context);
            }
            catch (Exception ex)
            {
                theoryData.ExpectedException.ProcessException(ex, context);
            }

            TestUtilities.AssertFailIfErrors(context);
        }

        public static TheoryData<CreateSignedHttpRequestTheoryData> CreateUClaimTheoryData
        {
            get
            {
                return new TheoryData<CreateSignedHttpRequestTheoryData>
                {
                    new CreateSignedHttpRequestTheoryData
                    {
                        First = true,
                        ExpectedClaim = SignedHttpRequestClaimTypes.U,
                        ExpectedClaimValue = "www.contoso.com",
                        HttpRequestUri = new Uri("https://www.contoso.com/path1?queryParam1=value1"),
                        TestId = "ValidU1",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.U,
                        ExpectedClaimValue = "www.contoso.com",
                        HttpRequestUri = new Uri("http://www.Contoso.com/"),
                        TestId = "ValidU2",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.U,
                        ExpectedClaimValue = "www.contoso.com",
                        HttpRequestUri = new Uri("https://www.contoso.com:443"),
                        TestId = "ValidU3",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.U,
                        ExpectedClaimValue = "www.contoso.com:81",
                        HttpRequestUri = new Uri("https://www.contoso.com:81"),
                        TestId = "ValidU4",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.U,
                        HttpRequestUri = new Uri("/relativePath", UriKind.Relative),
                        ExpectedException = new ExpectedException(typeof(SignedHttpRequestCreationException), "IDX23001"),
                        TestId = "InvalidRelativeUri",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        Payload = null,
                        ExpectedException = ExpectedException.ArgumentNullException(),
                        TestId = "NullPayload",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        HttpRequestUri = null,
                        ExpectedException = ExpectedException.ArgumentNullException(),
                        TestId = "NullUri",
                    },
                };
            }
        }

        [Theory, MemberData(nameof(CreatePClaimTheoryData))]
        public void CreatePClaim(CreateSignedHttpRequestTheoryData theoryData)
        {
            var context = TestUtilities.WriteHeader($"{this}.CreatePClaim", theoryData);
            try
            {
                var handler = new SignedHttpRequestHandlerPublic();
                var signedHttpRequestDescriptor = theoryData.BuildSignedHttpRequestDescriptor();

                handler.AddPClaimPublic(theoryData.Payload, signedHttpRequestDescriptor);
                var payload = JObject.Parse(handler.ConvertToJsonPublic(theoryData.Payload));

                if (!payload.ContainsKey(theoryData.ExpectedClaim))
                    context.AddDiff($"Payload doesn't contain the claim '{theoryData.ExpectedClaim}'");

                if (!IdentityComparer.AreStringsEqual(payload.Value<string>(theoryData.ExpectedClaim), theoryData.ExpectedClaimValue, context))
                    context.AddDiff($"Value of '{theoryData.ExpectedClaim}' claim is '{payload.Value<string>(theoryData.ExpectedClaim)}', but expected value was '{theoryData.ExpectedClaimValue}'");

                theoryData.ExpectedException.ProcessNoException(context);
            }
            catch (Exception ex)
            {
                theoryData.ExpectedException.ProcessException(ex, context);
            }

            TestUtilities.AssertFailIfErrors(context);
        }

        public static TheoryData<CreateSignedHttpRequestTheoryData> CreatePClaimTheoryData
        {
            get
            {
                return new TheoryData<CreateSignedHttpRequestTheoryData>
                {
                    new CreateSignedHttpRequestTheoryData
                    {
                        First = true,
                        ExpectedClaim = SignedHttpRequestClaimTypes.P,
                        ExpectedClaimValue = "/path1",
                        HttpRequestUri = new Uri("https://www.contoso.com/path1?queryParam1=value1"),
                        TestId = "ValidP1",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.P,
                        ExpectedClaimValue = "/path1/",
                        HttpRequestUri = new Uri("https://www.contoso.com/path1/"),
                        TestId = "ValidP2",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.P,
                        ExpectedClaimValue = "/path1",
                        HttpRequestUri = new Uri("https://www.contoso.com/path1"),
                        TestId = "ValidP3",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.P,
                        ExpectedClaimValue = "/path1",
                        HttpRequestUri = new Uri("http://www.contoso.com:81/path1"),
                        TestId = "ValidP4",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.P,
                        ExpectedClaimValue = "/pa%20th1",
                        HttpRequestUri = new Uri("http://www.contoso.com:81/pa th1"),
                        TestId = "ValidP5",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.P,
                        ExpectedClaimValue = "/",
                        HttpRequestUri = new Uri("http://www.contoso.com"),
                        TestId = "NoPath",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.P,
                        ExpectedClaimValue = "/relativePath",
                        HttpRequestUri = new Uri("/relativePath", UriKind.Relative),
                        TestId = "ValidRelativeUri",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        Payload = null,
                        ExpectedException = ExpectedException.ArgumentNullException(),
                        TestId = "NullPayload",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        HttpRequestUri = null,
                        ExpectedException = ExpectedException.ArgumentNullException(),
                        TestId = "NullUri",
                    },
                };
            }
        }

        [Theory, MemberData(nameof(CreateQClaimTheoryData))]
        public void CreateQClaim(CreateSignedHttpRequestTheoryData theoryData)
        {
            var context = TestUtilities.WriteHeader($"{this}.CreateQClaim", theoryData);
            try
            {
                var handler = new SignedHttpRequestHandlerPublic();
                var signedHttpRequestDescriptor = theoryData.BuildSignedHttpRequestDescriptor();

                handler.AddQClaimPublic(theoryData.Payload, signedHttpRequestDescriptor);
                var payload = JObject.Parse(handler.ConvertToJsonPublic(theoryData.Payload));

                if (!payload.ContainsKey(theoryData.ExpectedClaim))
                    context.AddDiff($"Payload doesn't contain the claim '{theoryData.ExpectedClaim}'");

                if (!IdentityComparer.AreStringsEqual(payload.Value<JArray>(theoryData.ExpectedClaim).ToString(Formatting.None), theoryData.ExpectedClaimValue, context))
                    context.AddDiff($"Value of '{theoryData.ExpectedClaim}' claim is '{payload.Value<JArray>(theoryData.ExpectedClaim).ToString(Formatting.None)}', but expected value was '{theoryData.ExpectedClaimValue}'");

                theoryData.ExpectedException.ProcessNoException(context);
            }
            catch (Exception ex)
            {
                theoryData.ExpectedException.ProcessException(ex, context);
            }

            TestUtilities.AssertFailIfErrors(context);
        }

        public static TheoryData<CreateSignedHttpRequestTheoryData> CreateQClaimTheoryData
        {
            get
            {
                return new TheoryData<CreateSignedHttpRequestTheoryData>
                {
                    new CreateSignedHttpRequestTheoryData
                    {
                        First = true,
                        ExpectedClaim = SignedHttpRequestClaimTypes.Q,
                        ExpectedClaimValue = $"[[\"queryParam1\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("queryParam1=value1")}\"]",
                        HttpRequestUri = new Uri("https://www.contoso.com/path1?queryParam1=value1"),
                        TestId = "ValidQ1",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.Q,
                        ExpectedClaimValue = $"[[\"queryParam1\",\"queryParam2\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("queryParam1=value1&queryParam2=value2")}\"]",
                        HttpRequestUri = new Uri("https://www.contoso.com/path1?queryParam1=value1&queryParam2=value2"),
                        TestId = "ValidQ2",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.Q,
                        ExpectedClaimValue = $"[[\"queryParam1\",\"queryParam2\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("queryParam1=value1&queryParam2=value2")}\"]",
                        HttpRequestUri = new Uri("https://www.contoso.com/path1?&queryParam1=value1&queryParam2=value2"),
                        TestId = "ValidQ3",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.Q,
                        ExpectedClaimValue = $"[[\"query%20Param1\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("query%20Param1=value1")}\"]",
                        HttpRequestUri = new Uri("https://www.contoso.com/path1?query Param1=value1"),
                        TestId = "ValidQ4",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.Q,
                        ExpectedClaimValue = $"[[\"query%20Param1%20\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("query%20Param1%20=value1%20")}\"]",
                        HttpRequestUri = new Uri("https://www.contoso.com/path1?query Param1 =value1%20"),
                        TestId = "ValidQ5",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.Q,
                        ExpectedClaimValue = $"[[\"queryParam1\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("queryParam1=value1")}\"]",
                        HttpRequestUri = new Uri("https://www.contoso.com/path1?&queryParam1=value1&query=Param2=value2"),
                        TestId = "ValidQ6",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.Q,
                        ExpectedClaimValue = $"[[],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("")}\"]",
                        HttpRequestUri = new Uri("https://www.contoso.com/path1"),
                        TestId = "ValidNoQueryParams1",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.Q,
                        ExpectedClaimValue = $"[[],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("")}\"]",
                        HttpRequestUri = new Uri("https://www.contoso.com/path1&"),
                        TestId = "ValidNoQueryParams2",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.Q,
                        ExpectedClaimValue = $"[[],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("")}\"]",
                        HttpRequestUri = new Uri("https://www.contoso.com/path1&t"),
                        TestId = "ValidNoQueryParams3",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.Q,
                        ExpectedClaimValue = $"[[],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("")}\"]",
                        HttpRequestUri = new Uri("https://www.contoso.com/path1&t="),
                        TestId = "ValidNoQueryParams4",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.Q,
                        ExpectedClaimValue = $"[[\"queryParam1\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("queryParam1=value1")}\"]",
                        HttpRequestUri = new Uri("https://www.contoso.com/path1?queryParam1=value1&repeated=repeated1&repeated=repeate2"),
                        TestId = "ValidRepeatedQ1",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.Q,
                        ExpectedClaimValue = $"[[\"queryParam1\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("queryParam1=value1")}\"]",
                        HttpRequestUri = new Uri("https://www.contoso.com/path1?&repeated=repeated1&queryParam1=value1&repeated=repeate2"),
                        TestId = "ValidRepeatedQ2",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.Q,
                        ExpectedClaimValue = $"[[\"queryParam1\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("queryParam1=value1")}\"]",
                        HttpRequestUri = new Uri("https://www.contoso.com/path1?&repeated=repeated1&repeated=repeate2&queryParam1=value1"),
                        TestId = "ValidRepeatedQ3",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.Q,
                        ExpectedClaimValue = $"[[\"queryParam1\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("queryParam1=value1")}\"]",
                        HttpRequestUri = new Uri("https://www.contoso.com/path1?&repeated=repeated1&repeated=repeate2&queryParam1=value1&repeated=repeate3"),
                        TestId = "ValidRepeatedQ4",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.Q,
                        ExpectedClaimValue = $"[[],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("")}\"]",
                        HttpRequestUri = new Uri("https://www.contoso.com/path1?&repeated=repeated1&repeated=repeate2"),
                        TestId = "RepeatedQEmpty",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.Q,
                        ExpectedClaimValue = $"[[\"queryParam1\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("queryParam1=value1")}\"]",
                        HttpRequestUri = new Uri("/relativePath?queryParam1=value1", UriKind.Relative),
                        TestId = "ValidRelativeUri",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        Payload = null,
                        ExpectedException = ExpectedException.ArgumentNullException(),
                        TestId = "NullPayload",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        HttpRequestUri = null,
                        ExpectedException = ExpectedException.ArgumentNullException(),
                        TestId = "NullUri",
                    },
                };
            }
        }

        [Theory, MemberData(nameof(CreateHClaimTheoryData))]
        public void CreateHClaim(CreateSignedHttpRequestTheoryData theoryData)
        {
            var context = TestUtilities.WriteHeader($"{this}.CreateHClaim", theoryData);
            try
            {
                var handler = new SignedHttpRequestHandlerPublic();
                var signedHttpRequestDescriptor = theoryData.BuildSignedHttpRequestDescriptor();

                handler.AddHClaimPublic(theoryData.Payload, signedHttpRequestDescriptor);
                var payload = JObject.Parse(handler.ConvertToJsonPublic(theoryData.Payload));

                if (!payload.ContainsKey(theoryData.ExpectedClaim))
                    context.AddDiff($"Payload doesn't contain the claim '{theoryData.ExpectedClaim}'");

                if (!IdentityComparer.AreStringsEqual(payload.Value<JArray>(theoryData.ExpectedClaim).ToString(Formatting.None), theoryData.ExpectedClaimValue, context))
                    context.AddDiff($"Value of '{theoryData.ExpectedClaim}' claim is '{payload.Value<JArray>(theoryData.ExpectedClaim).ToString(Formatting.None)}', but expected value was '{theoryData.ExpectedClaimValue}'");

                theoryData.ExpectedException.ProcessNoException(context);
            }
            catch (Exception ex)
            {
                theoryData.ExpectedException.ProcessException(ex, context);
            }

            TestUtilities.AssertFailIfErrors(context);
        }

        public static TheoryData<CreateSignedHttpRequestTheoryData> CreateHClaimTheoryData
        {
            get
            {
                return new TheoryData<CreateSignedHttpRequestTheoryData>
                {
                    new CreateSignedHttpRequestTheoryData
                    {
                        First = true,
                        ExpectedClaim = SignedHttpRequestClaimTypes.H,
                        ExpectedClaimValue = $"[[\"headername1\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("headername1: headerValue1")}\"]",
                        HttpRequestHeaders = new Dictionary<string, IEnumerable<string>>()
                        {
                            { "headerName1" , new List<string> { "headerValue1" } }
                        },
                        TestId = "ValidH1",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.H,
                        ExpectedClaimValue = $"[[\"headername1\",\"headername2\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("headername1: headerValue1\nheadername2: headerValue2")}\"]",
                        HttpRequestHeaders = new Dictionary<string, IEnumerable<string>>()
                        {
                            { "headerName1" , new List<string> { "headerValue1" } },
                            { "headerName2" , new List<string> { "headerValue2" } },
                        },
                        TestId = "ValidH2",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.H,
                        ExpectedClaimValue = $"[[\"headername1\",\"headername2\",\"headername3\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("headername1: headerValue1\nheadername2: headerValue2\nheadername3: headerValue3")}\"]",
                        HttpRequestHeaders = new Dictionary<string, IEnumerable<string>>()
                        {
                            { "headerName1" , new List<string> { "headerValue1" } },
                            { "headerName2" , new List<string> { "headerValue2" } },
                            { "headerName3" , new List<string> { "headerValue3" } },
                        },
                        TestId = "ValidH3",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.H,
                        ExpectedClaimValue = $"[[\"header name1\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("header name1: headerValue1")}\"]",
                        HttpRequestHeaders = new Dictionary<string, IEnumerable<string>>()
                        {
                            { "header Name1" , new List<string> { "headerValue1" } }
                        },
                        TestId = "ValidH4",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.H,
                        ExpectedClaimValue = $"[[],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("")}\"]",
                        HttpRequestHeaders = new Dictionary<string, IEnumerable<string>>()
                        {
                            { "" , new List<string> { "headerValue1" } }
                        },
                        TestId = "ValidH5",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.H,
                        ExpectedClaimValue = $"[[],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("")}\"]",
                        HttpRequestHeaders = new Dictionary<string, IEnumerable<string>>()
                        {
                            { "h1" , new List<string> { "" } }
                        },
                        TestId = "ValidH6",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.H,
                        ExpectedClaimValue = $"[[\"headername1\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("headername1: headerValue1")}\"]",
                        HttpRequestHeaders = new Dictionary<string, IEnumerable<string>>()
                        {
                            { "headerName1" , new List<string> { "headerValue1" } },
                            { SignedHttpRequestConstants.AuthorizationHeader , new List<string> { "exyxz..." } },
                        },
                        TestId = "ValidH7",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.H,
                        ExpectedClaimValue = $"[[\"headername1\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("headername1: headerValue1")}\"]",
                        HttpRequestHeaders = new Dictionary<string, IEnumerable<string>>()
                        {
                            { "headerName1" , new List<string> { "headerValue1" } }
                        },
                        TestId = "NoHeaders",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.H,
                        ExpectedClaimValue = $"[[\"headername1\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("headername1: headerValue1")}\"]",
                        HttpRequestHeaders = new Dictionary<string, IEnumerable<string>>()
                        {
                            { "headerName1" , new List<string> { "headerValue1" } },
                            { "headerName2" , new List<string> { "headerValue2", "headerValue10" } },
                        },
                        TestId = "ValidRepeatedH1",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.H,
                        ExpectedClaimValue = $"[[\"headername1\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("headername1: headerValue1")}\"]",
                        HttpRequestHeaders = new Dictionary<string, IEnumerable<string>>()
                        {
                            { "headerName2" , new List<string> { "headerValue2", "headerValue10" } },
                            { "headerName1" , new List<string> { "headerValue1" } },
                        },
                        TestId = "ValidRepeatedH2",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.H,
                        ExpectedClaimValue = $"[[],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("")}\"]",
                        HttpRequestHeaders = new Dictionary<string, IEnumerable<string>>()
                        {
                            { "headerName2" , new List<string> { "headerValue2", "headerValue10" } },
                        },
                        TestId = "ValidRepeatedH3",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.H,
                        ExpectedClaimValue = $"[[\"headername1\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("headername1: headerValue1")}\"]",
                        HttpRequestHeaders = new Dictionary<string, IEnumerable<string>>()
                        {
                            { "HeaDerName2" , new List<string> { "headerValue2" } },
                            { "headername2" , new List<string> { "headerValue10" } },
                            { "headerName1" , new List<string> { "headerValue1" } },
                        },
                        TestId = "ValidRepeatedH4",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.H,
                        ExpectedClaimValue = $"[[\"headername1\"],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("headername1: headerValue1")}\"]",
                        HttpRequestHeaders = new Dictionary<string, IEnumerable<string>>()
                        {
                            { "HeaDerName2" , new List<string> { "headerValue2" } },
                            { "headername2" , new List<string> { "headerValue10" } },
                            { "headerName1" , new List<string> { "headerValue1" } },
                            { "HEADERNAME2" , new List<string> { "headerValue22" } },
                        },
                        TestId = "ValidRepeatedH5",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.H,
                        ExpectedClaimValue = $"[[],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("")}\"]",
                        HttpRequestHeaders = new Dictionary<string, IEnumerable<string>>()
                        {
                            { "headerName1" , new List<string> { "headerValue1", "headerValue10" } },
                        },
                        TestId = "ValidRepeatedH6",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.H,
                        ExpectedClaimValue = $"[[],\"{SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash("")}\"]",
                        HttpRequestHeaders = new Dictionary<string, IEnumerable<string>>(),
                        TestId = "EmptyHeaders",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.H,
                        HttpRequestHeaders = null,
                        ExpectedException = ExpectedException.ArgumentNullException(),
                        TestId = "NullHeaders",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        Payload = null,
                        ExpectedException = ExpectedException.ArgumentNullException(),
                        TestId = "NullPayload",
                    },
                };
            }
        }

        [Theory, MemberData(nameof(CreateBClaimTheoryData))]
        public void CreateBClaim(CreateSignedHttpRequestTheoryData theoryData)
        {
            var context = TestUtilities.WriteHeader($"{this}.CreateBClaim", theoryData);
            try
            {
                var handler = new SignedHttpRequestHandlerPublic();
                var signedHttpRequestDescriptor = theoryData.BuildSignedHttpRequestDescriptor();

                handler.AddBClaimPublic(theoryData.Payload, signedHttpRequestDescriptor);
                var payload = JObject.Parse(handler.ConvertToJsonPublic(theoryData.Payload));

                if (!payload.ContainsKey(theoryData.ExpectedClaim))
                    context.AddDiff($"Payload doesn't contain the claim '{theoryData.ExpectedClaim}'");

                if (!IdentityComparer.AreStringsEqual(payload.Value<string>(theoryData.ExpectedClaim), theoryData.ExpectedClaimValue, context))
                    context.AddDiff($"Value of '{theoryData.ExpectedClaim}' claim is '{payload.Value<string>(theoryData.ExpectedClaim)}', but expected value was '{theoryData.ExpectedClaimValue}'");

                theoryData.ExpectedException.ProcessNoException(context);
            }
            catch (Exception ex)
            {
                theoryData.ExpectedException.ProcessException(ex, context);
            }

            TestUtilities.AssertFailIfErrors(context);
        }

        public static TheoryData<CreateSignedHttpRequestTheoryData> CreateBClaimTheoryData
        {
            get
            {
                return new TheoryData<CreateSignedHttpRequestTheoryData>
                {
                    new CreateSignedHttpRequestTheoryData
                    {
                        First = true,
                        ExpectedClaim = SignedHttpRequestClaimTypes.B,
                        ExpectedClaimValue = SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash(Encoding.UTF8.GetBytes("abcd")),
                        HttpRequestBody = Encoding.UTF8.GetBytes("abcd"),
                        TestId = "ValidB1",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.B,
                        ExpectedClaimValue = SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash(Encoding.UTF8.GetBytes("")),
                        HttpRequestBody = new byte[0],
                        TestId = "ValidB2",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = SignedHttpRequestClaimTypes.B,
                        ExpectedClaimValue = SignedHttpRequestTestUtils.CalculateBase64UrlEncodedHash(Encoding.UTF8.GetBytes("")),
                        HttpRequestBody = null,
                        TestId = "NullBytes",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        Payload = null,
                        ExpectedException = ExpectedException.ArgumentNullException(),
                        TestId = "NullPayload",
                    },
                };
            }
        }

        [Theory, MemberData(nameof(CreateNonceClaimTheoryData))]
        public void CreateNonceClaim(CreateSignedHttpRequestTheoryData theoryData)
        {
            var context = TestUtilities.WriteHeader($"{this}.CreateNonceClaim", theoryData);
            try
            {
                var handler = new SignedHttpRequestHandlerPublic();
                var signedHttpRequestDescriptor = theoryData.BuildSignedHttpRequestDescriptor();

                handler.AddNonceClaimPublic(theoryData.Payload, signedHttpRequestDescriptor);
                var payload = JObject.Parse(handler.ConvertToJsonPublic(theoryData.Payload));

                if (!payload.ContainsKey(theoryData.ExpectedClaim))
                    context.AddDiff($"Payload doesn't contain the claim '{theoryData.ExpectedClaim}'");

                if (theoryData.SignedHttpRequestCreationParameters.CustomNonceCreator != null)
                {
                    if (!IdentityComparer.AreStringsEqual(payload.Value<string>(theoryData.ExpectedClaim), theoryData.ExpectedClaimValue, context))
                        context.AddDiff($"Value of '{theoryData.ExpectedClaim}' claim is '{payload.Value<string>(theoryData.ExpectedClaim)}', but expected value was '{theoryData.ExpectedClaimValue}'");
                }

                theoryData.ExpectedException.ProcessNoException(context);
            }
            catch (Exception ex)
            {
                theoryData.ExpectedException.ProcessException(ex, context);
            }

            TestUtilities.AssertFailIfErrors(context);
        }

        public static TheoryData<CreateSignedHttpRequestTheoryData> CreateNonceClaimTheoryData
        {
            get
            {
                return new TheoryData<CreateSignedHttpRequestTheoryData>
                {
                    new CreateSignedHttpRequestTheoryData
                    {
                        First = true,
                        ExpectedClaim = SignedHttpRequestClaimTypes.Nonce,
                        TestId = "ValidDefaultNonce",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = "customNonce",
                        ExpectedClaimValue = "customNonceValue",
                        SignedHttpRequestCreationParameters = new SignedHttpRequestCreationParameters()
                        {
                            CustomNonceCreator = (IDictionary<string, object> payload, SignedHttpRequestDescriptor signedHttpRequestDescriptor) => payload.Add("customNonce", "customNonceValue"),
                        },
                        TestId = "ValidCustomNonce",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        Payload = null,
                        ExpectedException = ExpectedException.ArgumentNullException(),
                        TestId = "NullPayload",
                    },
                };
            }
        }

        [Theory, MemberData(nameof(CreateAdditionalClaimTheoryData))]
        public void CreateAdditionalClaim(CreateSignedHttpRequestTheoryData theoryData)
        {
            var context = TestUtilities.WriteHeader($"{this}.CreateAdditionalClaim", theoryData);
            try
            {
                var handler = new SignedHttpRequestHandlerPublic();
                var signedHttpRequestDescriptor = theoryData.BuildSignedHttpRequestDescriptor();

                var payloadString =  handler.CreateHttpRequestPayloadPublic(signedHttpRequestDescriptor);
                var payload = JObject.Parse(payloadString);

                if (theoryData.SignedHttpRequestCreationParameters.AdditionalClaimCreator != null)
                {
                    if (!payload.ContainsKey(theoryData.ExpectedClaim))
                        context.AddDiff($"Payload doesn't contain the claim '{theoryData.ExpectedClaim}'");

                    if (!IdentityComparer.AreStringsEqual(payload.Value<string>(theoryData.ExpectedClaim), theoryData.ExpectedClaimValue, context))
                        context.AddDiff($"Value of '{theoryData.ExpectedClaim}' claim is '{payload.Value<string>(theoryData.ExpectedClaim)}', but expected value was '{theoryData.ExpectedClaimValue}'");
                }
                else
                {
                    if (payload.ContainsKey(theoryData.ExpectedClaim))
                        context.AddDiff($"Payload shouldn't contain the claim '{theoryData.ExpectedClaim}'");
                }

                theoryData.ExpectedException.ProcessNoException(context);
            }
            catch (Exception ex)
            {
                theoryData.ExpectedException.ProcessException(ex, context);
            }

            TestUtilities.AssertFailIfErrors(context);
        }

        public static TheoryData<CreateSignedHttpRequestTheoryData> CreateAdditionalClaimTheoryData
        {
            get
            {
                return new TheoryData<CreateSignedHttpRequestTheoryData>
                {
                    new CreateSignedHttpRequestTheoryData
                    {
                        First = true,
                        ExpectedClaim = "customClaim",
                        ExpectedClaimValue = "customClaimValue",
                        SignedHttpRequestCreationParameters = new SignedHttpRequestCreationParameters()
                        {
                            CreateU = false,
                            CreateM = false,
                            CreateP = false,
                            AdditionalClaimCreator = (IDictionary<string, object> payload, SignedHttpRequestDescriptor signedHttpRequestDescriptor) => payload.Add("customClaim", "customClaimValue"),
                        },
                        TestId = "ValidAdditionalClaim",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        ExpectedClaim = "customClaim",
                        ExpectedClaimValue = "customClaimValue",
                        SignedHttpRequestCreationParameters = new SignedHttpRequestCreationParameters()
                        {
                            CreateU = false,
                            CreateM = false,
                            CreateP = false,
                        },
                        TestId = "DelegateNotSet",
                    },
                    new CreateSignedHttpRequestTheoryData
                    {
                        Payload = null,
                        ExpectedException = ExpectedException.ArgumentNullException(),
                        TestId = "NullPayload",
                    },
                };
            }
        }
    }

    public class CreateSignedHttpRequestTheoryData : TheoryDataBase
    {
        public SignedHttpRequestDescriptor BuildSignedHttpRequestDescriptor()
        {
            var httpRequestData = new HttpRequestData()
            {
                Body = HttpRequestBody,
                Uri = HttpRequestUri,
                Method = HttpRequestMethod,
                Headers = HttpRequestHeaders
            };

            var callContext = CallContext;
            if (callContext.PropertyBag == null)
                callContext.PropertyBag = new Dictionary<string, object>() { { "testId", TestId } };
            else
                callContext.PropertyBag.Add("testId", TestId);

            return new SignedHttpRequestDescriptor(Token, httpRequestData, SigningCredentials, SignedHttpRequestCreationParameters, callContext);
        }

        public CallContext CallContext { get; set; } = CallContext.Default;

        public object ExpectedClaimValue { get; set; }

        public string ExpectedClaim { get; set; }

        public List<string> ExpectedPayloadClaims { get; set; }

        public Uri HttpRequestUri { get; set; }

        public string HttpRequestMethod { get; set; }

        public IDictionary<string, IEnumerable<string>> HttpRequestHeaders { get; set; }

        public byte[] HttpRequestBody { get; set; }

        public SignedHttpRequestCreationParameters SignedHttpRequestCreationParameters { get; set; } = new SignedHttpRequestCreationParameters()
        {
            CreateB = true,
            CreateH = true,
            CreateM = true,
            CreateNonce = true,
            CreateP = true,
            CreateQ = true,
            CreateTs = true,
            CreateU = true
        };

        public Dictionary<string, object> Payload { get; set; } = new Dictionary<string, object>();

        public SigningCredentials SigningCredentials { get; set; } = SignedHttpRequestTestUtils.DefaultSigningCredentials;

        public string Token { get; set; } = SignedHttpRequestTestUtils.DefaultEncodedAccessToken;

        public string HeaderString { get; set; }

        public string PayloadString { get; set; }
    }
}

#pragma warning restore CS3016 // Arrays as attribute arguments is not CLS-compliant