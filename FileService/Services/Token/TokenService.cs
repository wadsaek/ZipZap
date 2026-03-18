// TokenService.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY, without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Microsoft.IdentityModel.Tokens;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;

namespace ZipZap.FileService.Services;

public class TokenService : ITokenService {
    private readonly RsaSecurityKey _key;

    public TokenService(RsaSecurityKey key) {
        _key = key;
    }

    public string GenerateToken(User user) {

        var expiration = DateTime.UtcNow.AddHours(5);
        Claim[] claims = [
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.Value.ToString()),
            new Claim("role",user.Role.ToString())
        ];
        var creds = new SigningCredentials(_key, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: "http://localhost:5210",
            audience: "http://localhost:5210",
            claims,
            expires: expiration,
            signingCredentials: creds
        );
        string tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return $"Bearer {tokenString}";
    }

    public bool TryGetUserId(string token, out UserId userId) {
        userId = default;

        var split = token.Split(' ');
        if (split.Length != 2 || split[0] != "Bearer") return false;
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(split[1])) return false;
        var jwt = handler.ReadJwtToken(split[1]);

        if (!Guid.TryParse(jwt.Subject, out var guid)) return false;
        userId = guid.ToUserId();
        return true;
    }
}
