using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CMaaS.Task.Model;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;

namespace CMaaS.Task.Service
{
    class DBUtil
    {

        public async Task<Document> CreateGroupTaskSet(GroupTaskSet gts)
        {
            ResourceResponse<Document> doc = await Client.CreateDocumentAsync(CollectionLink, gts);
            return doc;
        }

        public GroupTaskSet GetGroupTaskSetByID(string id)
        {
            var option = new FeedOptions { EnableCrossPartitionQuery = true };

            dynamic doc = Client.CreateDocumentQuery<Document>(CollectionLink, option)
                        .Where(x => x.Id == id).AsEnumerable().FirstOrDefault();

            GroupTaskSet convertedGTS = null;

            if (doc != null)
            {
                convertedGTS = doc;
            }
         
            return convertedGTS;
        }
        public async Task<List<GroupTaskSet>> GetGroupTaskSetByTenantAndCase (string caseid, string tenant)
        {
            var option = new FeedOptions { EnableCrossPartitionQuery = true };

            var query = from doc in Client.CreateDocumentQuery<GroupTaskSet>(CollectionLink, option)
                        where (doc.tenantid == tenant) && (doc.caseid == caseid) 
                        select doc;
    
            var gts = await QueryGroupTaskSets(query);
            List<GroupTaskSet> convertedGTS = gts.ToList();

            return convertedGTS;
        }

        private static async Task<IEnumerable<GroupTaskSet>> QueryGroupTaskSets<GroupTaskSet>(IQueryable<GroupTaskSet> query)
        {
            //this was added to support GetGroupTaskSetByTenantAndCase method so that query can be done asynchronously 
            var docQuery = query.AsDocumentQuery();
            var batches = new List<IEnumerable<GroupTaskSet>>();

            do
            {
                var batch = await docQuery.ExecuteNextAsync<GroupTaskSet>();
                batches.Add(batch);
            }

            while (docQuery.HasMoreResults);

            var docs = batches.SelectMany(b => b);
            return docs;
        }


        /*

        [AcceptVerbs("GET")]
        [HttpGet]
        public async Task<List<Models.Person>> GetActivePeopleByTenantandAppandType(string tenant, string appname, string objecttype)
        {
            var query = from doc in Client.CreateDocumentQuery<Models.Person>(CollectionLink)
                        where (doc.Tenant == tenant) && (doc.App == appname) && (doc.Type == objecttype)
                        select doc;

            var people = await QueryPeople(query);

            List<Person.Models.Person> convertedPeople = people.ToList();

            return convertedPeople;

        }

        [AcceptVerbs("GET")]
        [HttpGet]
        public async Task<List<Models.Person>> GetPeronsByName(string tenant, string appname, string objecttype, string Name)
        {

            //if the entity returned by Luis contains 2 names then split the name so that a query is done by first and last name
            if (Name.Contains(' ') == true)
            {
                string[] names = Name.Split(' ');
                var query = from doc in Client.CreateDocumentQuery<Models.Person>(CollectionLink)
                            where (doc.Tenant == tenant) && (doc.App == appname) && (doc.Type == objecttype) && (doc.FName == names[0]) && (doc.LastName == names[1])
                            select doc;

                var people = await QueryPeople(query);
                List<Person.Models.Person> convertedPeople = people.ToList();

                return convertedPeople;
            }
            else
            //if the entity returned by LUIS contains only 1 name then search the last name
            {
                var query = from doc in Client.CreateDocumentQuery<Models.Person>(CollectionLink)
                            where (doc.Tenant == tenant) && (doc.App == appname) && (doc.Type == objecttype) && (doc.LastName == Name)
                            select doc;

                var people = await QueryPeople(query);
                List<Person.Models.Person> convertedPeople = people.ToList();

                return convertedPeople;
            }


        } */

        //private static async Task<IEnumerable<Person>> QueryPeople<Person>(IQueryable<Person> query)
        //{
        //    //this was added to support GetActivePeopleByTenantandAppandType method so that query can be done asynchronously 
        //    var docQuery = query.AsDocumentQuery();
        //    var batches = new List<IEnumerable<Person>>();

        //    do
        //    {
        //        var batch = await docQuery.ExecuteNextAsync<Person>();
        //        batches.Add(batch);
        //    }

        //    while (docQuery.HasMoreResults);

        //    var docs = batches.SelectMany(b => b);
        //    return docs;
        //}


        public async Task<Boolean> UpdateGTSById(GroupTaskSet updatedGTS)
        {
            try
            {
                Boolean result = false;
                string id = updatedGTS.id;
                var option = new FeedOptions { EnableCrossPartitionQuery = true };

                dynamic doc = Client.CreateDocumentQuery<Document>(CollectionLink, option)
                    .Where(d => d.Id == id).AsEnumerable().FirstOrDefault();

                if (doc != null)
                {
                    ResourceResponse<Document> x = await Client.ReplaceDocumentAsync(doc.SelfLink, updatedGTS);
                    if (x.StatusCode.ToString() == "OK")
                    {
                        result = true;
                    }
                    else
                    {
                        result = false;
                    }
                }
                else
                {
                    result = false;
                }

                return result;
            }
            catch (Exception e)
            {
                Boolean errResult = false;
                return errResult;
            }

        }

        private static Uri _collectionLink;

        private static Uri CollectionLink
        {
            get
            {
                if (_collectionLink == null)
                {
                    _collectionLink = UriFactory.CreateDocumentCollectionUri("bloomskyHealth", "Task");
                }
                return _collectionLink;
            }
        }

        private static Uri _documentLink;

        private static Uri DocumentLink
        {
            get
            {
                if (_documentLink == null)
                {
                    _documentLink = UriFactory.CreateDocumentCollectionUri("bloomskyHealth", "Task");
                }
                return _collectionLink;
            }
        }


        private static DocumentClient _client;

        private static DocumentClient Client
        {
            get
            {
                if (_client == null)
                {
                    string endpoint = Environment.GetEnvironmentVariable("endpoint");
                    string authKey = Environment.GetEnvironmentVariable("authKey");
                    Uri endpointUri = new Uri(endpoint);
                    _client = new DocumentClient(endpointUri, authKey);
                }

                return _client;
            }
        }
    }
}
