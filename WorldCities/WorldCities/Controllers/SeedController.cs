﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using WorldCities.Data;
using WorldCities.Data.Models;

namespace WorldCities.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class SeedController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public SeedController(
            ApplicationDbContext context, 
            IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet]
        public async Task<ActionResult> Import()
        {
            // previne ambientes de producao de rodar este metodo
            if (!_env.IsDevelopment())
            {
                throw new SecurityException("Not allowed");
            }

            string path = Path.Combine(
                _env.ContentRootPath,
                "Data/Source/worldcities.xlsx");

            using FileStream stream = System.IO.File.OpenRead(path);
            using ExcelPackage excelPackage = new ExcelPackage(stream);

            ExcelWorksheet worksheet = excelPackage.Workbook.Worksheets[0];

            int nEndRow = worksheet.Dimension.End.Row;

            int numberOfCountriesAdded = 0;
            int numberOfCitiesAdded = 0;

            Dictionary<string, Country> countriesByName = _context.Countries
                .AsNoTracking()
                .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

            for (int nRow = 2; nRow < nEndRow; nRow++)
            {
                ExcelRange row = worksheet.Cells[
                    nRow, 1, nRow, worksheet.Dimension.End.Column];

                string countryName = row[nRow, 5].GetValue<string>();
                string iso2 = row[nRow, 6].GetValue<string>();
                string iso3 = row[nRow, 7].GetValue<string>();

                if (countriesByName.ContainsKey(countryName))
                {
                    continue;
                }

                Country country = new Country
                {
                    Name = countryName,
                    ISO2 = iso2,
                    ISO3 = iso3
                };

                await _context.Countries.AddAsync(country);

                countriesByName.Add(countryName, country);

                numberOfCountriesAdded++;
            }

            if (numberOfCountriesAdded > 0)
            {
                await _context.SaveChangesAsync();
            }

            var cities = _context.Cities
                .AsNoTracking()
                .ToDictionary(x => (
                    Name: x.Name,
                    Lat: x.Lat,
                    Lon: x.Lon,
                    CountryId: x.CountryId));

            for (int nRow = 2; nRow < nEndRow; nRow++)
            {
                var row = worksheet.Cells[
                    nRow, 1, nRow, worksheet.Dimension.End.Column];
                var name = row[nRow, 1].GetValue<string>();
                var nameAscii = row[nRow, 2].GetValue<string>();
                var lat = row[nRow, 3].GetValue<decimal>();
                var lon = row[nRow, 4].GetValue<decimal>();
                var countryName = row[nRow, 5].GetValue<string>();

                var countryId = countriesByName[countryName].Id;

                if (cities.ContainsKey((
                    Name: name,
                    Lat: lat,
                    Lon: lon,
                    CountryId: countryId)))
                {
                    continue;
                }

                var city = new City
                {
                    Name = name,
                    Name_ASCII = nameAscii,
                    Lat = lat,
                    Lon = lon,
                    CountryId = countryId
                };

                _context.Cities.Add(city);

                numberOfCitiesAdded++;
            }

            if (numberOfCitiesAdded > 0)
            {
                await _context.SaveChangesAsync();
            }

            return new JsonResult(new
            {
                Cities = numberOfCitiesAdded,
                Countries = numberOfCountriesAdded
            });
        }
    }
}