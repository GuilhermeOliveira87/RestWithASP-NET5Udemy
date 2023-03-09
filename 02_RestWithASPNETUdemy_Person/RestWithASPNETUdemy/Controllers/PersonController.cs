﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RestWithASPNETUdemy.Model;
using RestWithASPNETUdemy.Services;

namespace RestWithASPNETUdemy.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PersonController : ControllerBase {

        private readonly ILogger<PersonController> _logger;

        private IPersonServices _personService;

        public PersonController(ILogger<PersonController> logger, IPersonServices personService) {
            _logger = logger;
            _personService = personService;
        }

        [HttpGet]

        public IActionResult Get() {
            return Ok(_personService.FindAll());
        }

        [HttpGet("{id}")]

        public IActionResult Get(long id) {
            var person = _personService.FindByID(id);
            if (person == null) return NotFound();
            return Ok(person);
        }

        [HttpPost]

        public IActionResult Post([FromBody] Person person ) 
        {
            
            if (person == null) return BadRequest();
            return Ok(_personService.Create(person));
        }

        [HttpPut]

        public IActionResult Put([FromBody] Person person) {

            if (person == null) return BadRequest();
            return Ok(_personService.Create(person));
        }

        [HttpDelete("{id}")]

        public IActionResult Delete(long id) 
        {
            _personService.Delete(id);
            return NoContent();
        }


    }
}
