using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

    namespace Taslow.Project.Service
    {
        class SvcUtil
        {
            public string Create(string tenant)
            {
                using (SHA256 _sha256 = SHA256.Create())
                {
                    byte[] bytehash = _sha256.ComputeHash(Encoding.UTF8.GetBytes(tenant));
                    string hashvalue = BitConverter.ToString(bytehash);
                    return hashvalue;
                }


            }
        }
    }

