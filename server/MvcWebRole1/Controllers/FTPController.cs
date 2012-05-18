using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using MvcWebRole1.Models;

namespace MvcWebRole1.Controllers
{
    public class FTPController : Controller
    {
        
        //
        //POST: /FTP/Setup

        [HttpPost]
        public ActionResult Setup(FTPModel model) 
        {
            return View();
        }

        //
        //GET : /FTP/Setup
        public ActionResult Setup()
        {
            return View();
        }

        //
        //GET : /FTP/DeleteUser

        public ActionResult DeleteUser() 
        {
            return View();
        }
        //
        //POST: /FTP/DeleteUser
        [HttpPost]
        public ActionResult DeleteUser(FTPModel model) {
            Membership.DeleteUser(model.UserName);
            //TODO erase user data
            return View();
        }
        //
        // GET: /FTP/

        public ActionResult Index()
        {
            return View();
        }

        //
        // GET: /FTP/Details/5

        public ActionResult Details(int id)
        {
            return View();
        }

        //
        // GET: /FTP/Create

        public ActionResult Create()
        {
            return View();
        } 

        //
        // POST: /FTP/Create

        [HttpPost]
        public ActionResult Create(FormCollection collection)
        {
            try
            {
                // TODO: Add insert logic here

                return RedirectToAction("Index");
            }
            catch
            {
                return View();
            }
        }
        
        //
        // GET: /FTP/Edit/5
 
        public ActionResult Edit(int id)
        {
            return View();
        }

        //
        // POST: /FTP/Edit/5

        [HttpPost]
        public ActionResult Edit(int id, FormCollection collection)
        {
            try
            {
                // TODO: Add update logic here
 
                return RedirectToAction("Index");
            }
            catch
            {
                return View();
            }
        }

        //
        // GET: /FTP/Delete/5
 
        public ActionResult Delete(int id)
        {
            return View();
        }

        //
        // POST: /FTP/Delete/5

        [HttpPost]
        public ActionResult Delete(int id, FormCollection collection)
        {
            try
            {
                // TODO: Add delete logic here
 
                return RedirectToAction("Index");
            }
            catch
            {
                return View();
            }
        }
    }
}
