﻿using Endeksa.BackgroundServices;
using Endeksa.Models;
using Endeksa.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Endeksa.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class ClientInfoController : ControllerBase
    {
        private readonly RabbitMQClientService _rabbitmqClientService;
        private readonly RabbitMQPublisher _rabbitMQPublisher;
        private readonly RedisService _redisService;
        private readonly IDatabase _cache;
        private readonly ILogger<ClientInfoController> _logger;
        public ClientInfoController(RabbitMQClientService rabbitmqClientService, RabbitMQPublisher rabbitMQPublisher, RedisService redisService, ILogger<ClientInfoController> logger, IDatabase cache)
        {
            _rabbitmqClientService = rabbitmqClientService;
            _rabbitMQPublisher = rabbitMQPublisher;
           // _cache = _redisService.GetDb(1);
            _redisService = redisService;
            _logger = logger;
            _cache = cache;
        }

        [HttpGet]
        public ActionResult<UserLocation> CheckIp()
        {
            string ip = GetUserIP();
            string location = GetLocation(ip);

            if (_redisService.db.HashExists(_redisService.hashKey,ip))
            {
                _logger.LogInformation($"ip adresi rediste var:{ip}");
            }
            else
            {
                _logger.LogInformation($"ip adresi rediste yok:{ip}");
            }

            return Ok(new UserLocation { IP = ip, Location = location });
        }

        [HttpPost]
        public ActionResult AddIP(string ip)
        {
            // IP adresinin konumunu çek
            string location = GetLocation(ip);

            // IP adresini Redis'te kaydet
            if (!_redisService.isKeyExist(ip))
            {
                _rabbitMQPublisher.Publish(new UserIPDetectedEvent() { IP = ip, City = location });
            }
            return Ok(new UserLocation { IP = ip, Location = location });

            return Ok();
        }

        [HttpGet]
        /*swagger error
        //[Authorize]
        //[SwaggerOperation(
        //    Summary = "Get User IP and location",
        //    Description = "Get User and location by accessing an external API",
        //    OperationId = "GetUserAndLocation",
        //    Tags = new[] {"IP"}
        //    )]
        */
        public ActionResult<UserLocation> GetIP()
        {
            // _rabbitmqClientService.Connect();

            //kullanıcının ip adresi alınır.
            string ip = GetUserIP();
            //IP adresinin kullanılarak istekte bulunan kullanıcının konum bilgileri alınır.
            string location = GetLocation(ip);
            //if (await _cache.KeyExistsAsync(RedisService.IpKey))
            //{
            //    _rabbitMQPublisher.Publish(new UserIPDetectedEvent() { IP = ip, City = location });
            //}
            // _logger.LogInformation("ip adresi cachte bulundu.");

            if (!_redisService.isKeyExist(ip))
            {
                _rabbitMQPublisher.Publish(new UserIPDetectedEvent() { IP = ip, City = location });
            }
            return Ok(new UserLocation { IP = ip, Location = location });

        }





        /// <summary>
        /// Gelen isteğin IP adresini api'ye bağlanarak bulan method. Json formatında bir değer alır ve geriye string türünde değer döndürür. Gelen değer GetIP metodunda çalıştırılır.
        /// </summary>
        /// <returns></returns>
        private string GetUserIP()
        {
            string ip = "";
            try
            {
                //IP adresi için api çağrısı yapılır.
                string apiUrl = "http://api.ipstack.com/check?access_key=b13be0da105774096c8297334ba9b2ef\r\n";
                var json = new WebClient().DownloadString(apiUrl);
                var data = JObject.Parse(json);

                //IP adresi alınır
                ip = data["ip"].ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return ip;
        }
        /// <summary>
        /// Gelen isteğin sahip olduğu ip adresine bağlı olarak geriye ülke ve şehir bilgisini döndürür. Json formatında bir değer alır ve geriye string türünde değer döndürür. Gelen değer GetIP metodunda çalıştırılır.
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        private string GetLocation(string ip)
        {
            string location = "";
            try
            {
                //Konum bilgilerinin ip adresi kullanılarak gelmesi için api çağrısı yapılır.
                string apiUrl = $"http://api.ipstack.com/{ip}?access_key=b13be0da105774096c8297334ba9b2ef\r\n";
                var json = new WebClient().DownloadString(apiUrl);
                var data = JObject.Parse(json);

                //Şehir ve ülke bilgileri alınır
                string city = data["city"].ToString();
                string country = data["country_name"].ToString();

                location = $"{city}, {country}";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return location;
        }
    }
}
