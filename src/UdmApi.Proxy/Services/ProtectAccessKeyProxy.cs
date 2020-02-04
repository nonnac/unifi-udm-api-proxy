﻿using System;
using System.IO;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using UdmApi.Proxy.Helpers;
using UdmApi.Proxy.Sessions;

namespace UdmApi.Proxy.Services
{
    public class ProtectAccessKeyProxy : IServiceProxy
    {
        private readonly Uri _udmHost;
        private readonly SsoSessionCache _sessionCache;

        public ProtectAccessKeyProxy(IConfiguration configuration, SsoSessionCache sessionCache)
        {
            _udmHost = configuration.GetValue<Uri>("Udm:Uri");
            _sessionCache = sessionCache;
        }

        public bool DisableTlsVerification() => true;

        public bool Matches(HttpRequest request) => request.TryGetAuthorizationHeader(out var currentToken) // Only handled active sessions that we know about.
                                                    && _sessionCache.TryGet(currentToken, out _)
                                                    && request.Path.Equals("/api/auth/access-key");

        public void ModifyRequest(HttpRequest originalRequest, HttpRequestMessage proxyRequest)
        {
            var builder = new UriBuilder(_udmHost)
            {
                Path = "/proxy/protect/api/bootstrap",
                Query = originalRequest.QueryString.ToString()
            };

            // Gives a 404 when the token cookie is sent for some reason.
            proxyRequest.Headers.Remove("Cookie");
            proxyRequest.Content?.Headers.Remove("Cookie");

            proxyRequest.RequestUri = builder.Uri;

            if (originalRequest.TryGetAuthorizationHeader(out var token)
                && _sessionCache.TryGet(token, out var currentToken))
            {
                proxyRequest.Headers.Add("Cookie", $"TOKEN={currentToken}");
            }
        }

        public void ModifyResponseBody(HttpRequest originalRequest, Stream responseBody)
        {
        }

        public void ModifyResponse(HttpRequest originalRequest, HttpResponse response)
        {
            if (originalRequest.TryGetAuthorizationHeader(out var token)
                && response.TryGetSetCookeToken(out var currentToken))
            {
                _sessionCache.Update(token, currentToken);
            }
        }
    }
}