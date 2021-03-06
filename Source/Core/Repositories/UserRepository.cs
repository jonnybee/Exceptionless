﻿#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using ServiceStack.CacheAccess;

namespace Exceptionless.Core {
    public class UserRepository : MongoRepositoryWithIdentity<User>, IUserRepository {
        public UserRepository(MongoDatabase database, ICacheClient cacheClient = null) : base(database, cacheClient) {}

        public const string CollectionName = "user";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        public new static class FieldNames {
            public const string Id = "_id";
            public const string IsEmailAddressVerified = "IsEmailAddressVerified";
            public const string EmailNotificationsEnabled = "EmailNotificationsEnabled";
            public const string OrganizationIds = "OrganizationIds";
            public const string OAuthAccounts_Provider = "OAuthAccounts.Provider";
            public const string OAuthAccounts_ProviderUserId = "OAuthAccounts.ProviderUserId";
        }

        protected override void InitializeCollection(MongoCollection<User> collection) {
            base.InitializeCollection(collection);

            collection.CreateIndex(IndexKeys<User>.Ascending(u => u.OrganizationIds));
            collection.CreateIndex(IndexKeys<User>.Ascending(u => u.EmailAddress), IndexOptions.SetUnique(true));
            collection.CreateIndex(IndexKeys.Ascending(FieldNames.OAuthAccounts_Provider, FieldNames.OAuthAccounts_ProviderUserId), IndexOptions.SetUnique(true).SetSparse(true));
            collection.CreateIndex(IndexKeys<User>.Ascending(u => u.Roles));
        }

        protected override void ConfigureClassMap(BsonClassMap<User> cm) {
            base.ConfigureClassMap(cm);
            cm.GetMemberMap(p => p.OrganizationIds).SetSerializationOptions(new ArraySerializationOptions(new RepresentationSerializationOptions(BsonType.ObjectId)));
            cm.GetMemberMap(c => c.IsActive).SetIgnoreIfDefault(true);
            cm.GetMemberMap(c => c.IsEmailAddressVerified).SetIgnoreIfDefault(true);
            cm.GetMemberMap(c => c.Password).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.PasswordResetToken).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.PasswordResetTokenExpiration).SetIgnoreIfDefault(true);
            cm.GetMemberMap(c => c.Salt).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.VerifyEmailAddressToken).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.VerifyEmailAddressTokenExpiration).SetIgnoreIfDefault(true);
        }

        public User GetByEmailAddress(string emailAddress) {
            if (Cache == null)
                return FirstOrDefault(u => u.EmailAddress == emailAddress);

            var result = Cache.Get<User>(GetScopedCacheKey(emailAddress));
            if (result == null) {
                result = FirstOrDefault(u => u.EmailAddress == emailAddress);
                if (result != null)
                    Cache.Set(GetScopedCacheKey(emailAddress), result, TimeSpan.FromMinutes(5));
            }

            return result;
        }

        public User GetByVerifyEmailAddressToken(string token) {
            if (String.IsNullOrEmpty(token))
                return null;

            return Where(Query<User>.EQ(u => u.VerifyEmailAddressToken, token)).FirstOrDefault();
        }

        // TODO: Have this return a limited subset of user data.
        public IQueryable<User> GetByOrganizationId(string id) {
            // TODO Cache this.
            return Where(Query.In(FieldNames.OrganizationIds, new List<BsonValue> {
                new BsonObjectId(new ObjectId(id))
            }));
        }

        public override void InvalidateCache(User entity) {
            if (Cache == null)
                return;

            //TODO: We should look into getting the original entity and reset the cache on the original email address as it might have changed.
            InvalidateCache(entity.EmailAddress);

            base.InvalidateCache(entity);
        }
    }
}