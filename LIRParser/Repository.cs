using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LIRParser
{
    public class Repository<T>
    {
        private MongoCollection<T> _collect;
        public Repository(string connStr)
        {
            _collect = Db<T>.GetInstance(connStr).collect;
        }

        public IEnumerable<T> GetAll()
        {
            return _collect.FindAll();
        }
        public void SaveAll(IEnumerable<T> toSave)
        {
            foreach (var item in toSave)
            {
                _collect.Save(item);
            }
        }
        public void Save(T item)
        {
            _collect.Save(item);
        }


        //=============Database====================
        class Db<T> : MongoDatabase
        {
            public MongoCollection<T> collect;
            public Db(MongoServer serv, string name, MongoDatabaseSettings settings)
                : base(serv, name, settings)
            {
                collect = this.GetCollection<T>(typeof(T).Name);
            }

            public static Db<T> GetInstance(string connStr)
            {
                MongoUrl murl = new MongoUrl(connStr);
                var cli = new MongoClient(murl);
                var serv = cli.GetServer();
                return new Db<T>(serv, murl.DatabaseName, new MongoDatabaseSettings());
            }
        }
    }
}
