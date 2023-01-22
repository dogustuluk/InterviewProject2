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
        private readonly ILogger<ClientInfoController> _logger;
        private readonly IpDetectorService _ipDetectorService;
        public ClientInfoController(RabbitMQClientService rabbitmqClientService, RabbitMQPublisher rabbitMQPublisher, RedisService redisService, ILogger<ClientInfoController> logger, IpDetectorService ipDetectorService)
        {
            _rabbitmqClientService = rabbitmqClientService;
            _rabbitMQPublisher = rabbitMQPublisher;
            _redisService = redisService;
            _logger = logger;
            _ipDetectorService = ipDetectorService;
        }

        [HttpPost]
        public ActionResult AddIP(string ip)
        {
            // IP adresinin konumunu çek
            var location = _redisService.GetValue(ip);
            bool redis = string.IsNullOrEmpty(location) == false;
            
            if (string.IsNullOrEmpty(location))
            {
                location = _ipDetectorService.GetLocation(ip);
                // IP adresini Redis'te kaydet
                _rabbitMQPublisher.Publish(new UserIPDetectedEvent() { IP = ip, City = location });
            }
            return Ok(new UserLocation { IP = ip, Location = location, Message = redis ? "Mesaj redisten alındı" : "Mesaj Redise kaydedildi." });

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
            string ip = _ipDetectorService.GetUserIP();
            //IP adresinin kullanılarak istekte bulunan kullanıcının konum bilgileri alınır.
            string location = _ipDetectorService.GetLocation(ip);

            _rabbitMQPublisher.Publish(new UserIPDetectedEvent() { IP = ip, City = location });

            return Ok(new UserLocation { IP = ip, Location = location });

        }
    }
}
