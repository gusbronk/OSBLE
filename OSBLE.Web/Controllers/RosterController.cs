﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using LumenWorks.Framework.IO.Csv;
using OSBLE.Attributes;
using OSBLE.Models.Assignments;
using OSBLE.Models.Courses;
using OSBLE.Models.Users;
using OSBLE.Models.AbstractCourses;
using OSBLE.Models.AbstractCourses.Course;
using OSBLE.Models.HomePage; //yc: added for notifcations
using OSBLE.Utility;
using System.Net.Mail;
using OSBLEPlus.Logic.Utility;
using System.Web.Script.Serialization;

namespace OSBLE.Controllers
{
#if !DEBUG
    [RequireHttps]
#endif
    [OsbleAuthorize]
    [RequireActiveCourse]
    public class RosterController : OSBLEController
    {
        public RosterController()
        {
            ViewBag.CurrentTab = "Users";
            ViewBag.ActiveCourseUser = ActiveCourseUser;

            ViewBag.HideMail = OSBLE.Utility.DBHelper.GetAbstractCourseHideMailValue(ActiveCourseUser.AbstractCourseID);
        }

        public class RosterEntry
        {
            public string Identification
            {
                get;
                set;
            }

            public int Section
            {
                get;
                set;
            }

            public string Name
            {
                get;
                set;
            }
            public string Email
            {
                get;
                set;
            }
        }

        public class UsersBySection
        {
            public string SectionNumber
            {
                get;
                set;
            }

            public List<UsersByRole> UsersByRole
            {
                get;
                set;
            }

        }

        public class UsersByRole
        {
            public string RoleName
            {
                get;
                set;
            }

            public List<UserProfile> Users
            {
                get;
                set;
            }

            public int Count
            {
                get;
                set;
            }
        }
        // GET: /Roster/
        //[CanModifyCourse]
        [CanGradeCourse]
        public ActionResult Index()
        {
            //Get all users for the current class
            var users = (from c in db.CourseUsers
                         where c.AbstractCourseID == ActiveCourseUser.AbstractCourseID
                         select c);

            var usersGroupedBySection = users.GroupBy(CourseUser => CourseUser.Section).OrderBy(CourseUser => CourseUser.Key).ToList();

            List<UsersBySection> usersBySections = new List<UsersBySection>();


            //yc this portion is used to populate a white table relative to the current course 
            //this information should only be visible to instructors/admins (currently all students see this information

            //Get all the WhiteTabled Users for the current class 
            var WTusers = (from d in db.WhiteTableUsers
                           where d.CourseID == ActiveCourseUser.AbstractCourseID
                           select d);

            var WTusersGroupedByCourseID = WTusers.GroupBy(WhiteTableUsers => WhiteTableUsers.CourseID).OrderBy(WhiteTableUsers => WhiteTableUsers.Key).ToList();

            List<WhiteTableUser> WTup = new List<WhiteTableUser>();

            foreach (var WTu in WTusers)
            {
                WTup.Add(WTu);
            }

            //Remove duplicates that may slip in 
            WTup = WTup.Distinct().ToList();
            ViewBag.WhiteTableUsers = WTup;

            //\FW

            foreach (var section in usersGroupedBySection)
            {
                UsersBySection userBySection = new UsersBySection();
                userBySection.SectionNumber = section.Key.ToString();
                List<UsersByRole> usersByRoles = new List<UsersByRole>();

                //Get all the users for each role
                List<AbstractRole> roles = new List<AbstractRole>();

                if (ActiveCourseUser.AbstractCourse is Course)
                {
                    // Set custom role order for display
                    List<CourseRole.CourseRoles> rolesOrder = new List<CourseRole.CourseRoles>();

                    foreach (CourseRole.CourseRoles role in Enum.GetValues(typeof(CourseRole.CourseRoles)).Cast<CourseRole.CourseRoles>())
                    {
                        rolesOrder.Add(role);
                    }

                    foreach (CourseRole.CourseRoles r in rolesOrder)
                    {
                        roles.Add(db.CourseRoles.Find((int)r));

                    }
                }
                else
                { // Community
                    // Set custom role order for display
                    List<CommunityRole.OSBLERoles> rolesOrder = new List<CommunityRole.OSBLERoles>();

                    int i = (int)CommunityRole.OSBLERoles.Leader;
                    while (Enum.IsDefined(typeof(CommunityRole.OSBLERoles), i))
                    {
                        rolesOrder.Add((CommunityRole.OSBLERoles)i);
                        i++;
                    }

                    foreach (CommunityRole.OSBLERoles r in rolesOrder)
                    {
                        roles.Add(db.CommunityRoles.Find((int)r));
                    }
                }

                foreach (AbstractRole role in roles)
                {
                    UsersByRole usersByRole = new UsersByRole();
                    usersByRole.RoleName = role.Name;
                    usersByRole.Users = new List<UserProfile>(from c in section
                                                              where role.ID == c.AbstractRole.ID
                                                              orderby c.UserProfile.LastName
                                                              select c.UserProfile
                                                              );
                    usersByRole.Count = usersByRole.Users.Count;

                    usersByRoles.Add(usersByRole);
                }

                //reverse it so the least important people are first

                userBySection.UsersByRole = usersByRoles;

                usersBySections.Add(userBySection);
            }

            ViewBag.UsersBySections = usersBySections;

            ViewBag.CanEditSelf = CanModifyOwnLink(ActiveCourseUser);

            if (Request.Params["notice"] != null)
            {
                ViewBag.Notice = Request.Params["notice"];
            }

            return View();
        }

        [HttpPost]
        [CanModifyCourse]
        [NotForCommunity]
        public ActionResult ImportRoster(HttpPostedFileBase file)
        {
            string extension = Path.GetExtension(file.FileName);

            if (((file != null) && (file.ContentLength > 0)) && !String.Equals(".csv", extension))
            {
                ViewBag.Error = "OSBLE was unable to import the selected file.\nCurrently OSBLE+ only accepts comma-separated ('.csv') files.\nPlease choose a roster in the .csv format and try again.";
                return View("RosterError");
            }

            if ((file != null) && (file.ContentLength > 0))
            {
                // Save file into session
                MemoryStream memStream = new MemoryStream();
                file.InputStream.CopyTo(memStream);
                Cache["RosterFile"] = memStream.ToArray();

                //reset position after read
                file.InputStream.Position = 0;
                Stream s = file.InputStream;
                List<string> headers = getRosterHeaders(s);

                if (headers.Count() == 0)
                {
                    ViewBag.Error = "OSBLE was unable to import the selected file. Check the formatting of your roster's header to ensure  that all rows have the same number of columns.";
                    return View("RosterError");
                }

                file.InputStream.Seek(0, 0);

                string guessedSection = null;
                string guessedIdentification = null;
                string guessedName = null;
                string guessedName2 = null;
                string guessedEmail = null;

                // Guess headers for section and identification
                foreach (string header in headers)
                {
                    if (guessedSection == null)
                    {
                        if (Regex.IsMatch(header, "section", RegexOptions.IgnoreCase))
                        {
                            guessedSection = header;
                        }
                    }

                    if (guessedIdentification == null)
                    {
                        if (Regex.IsMatch(header, "\\bident", RegexOptions.IgnoreCase)
                            || Regex.IsMatch(header, "\\bid\\b", RegexOptions.IgnoreCase)
                            || Regex.IsMatch(header, "\\bnumber\\b", RegexOptions.IgnoreCase)
                            )
                        {
                            guessedIdentification = header;
                        }
                    }

                    if (guessedName == null)
                    {
                        if (Regex.IsMatch(header, "name", RegexOptions.IgnoreCase))
                        {
                            guessedName = header;
                        }
                    }
                    else if (guessedName2 == null)
                    {
                        if (Regex.IsMatch(header, "name", RegexOptions.IgnoreCase))
                        {
                            guessedName2 = header;
                        }
                    }
                    if (guessedEmail == null)
                    {
                        if (Regex.IsMatch(header, "email", RegexOptions.IgnoreCase) || Regex.IsMatch(header, "e-mail", RegexOptions.IgnoreCase))
                        {
                            guessedEmail = header;
                        }
                    }
                }

                ViewBag.Headers = headers;
                ViewBag.GuessedSection = guessedSection;
                ViewBag.GuessedIdentification = guessedIdentification;
                ViewBag.GuessedName = guessedName;
                ViewBag.GuessedName2 = guessedName2;
                ViewBag.GuessedEmail = guessedEmail;

                return View();
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [CanModifyCourse]
        [NotForCommunity]
        public ActionResult ApplyRoster(string idColumn, string sectionColumn, string nameColumn, string name2Column, string emailColumn)
        {
            //Minimum info: ID
            //Optional info: First Name, Last Name, Section and Username/Email

            byte[] rosterContent = (byte[])Cache["RosterFile"];
            int rosterCount = 0;
            int wtCount = 0;
            bool hasSectionColumn = !String.IsNullOrEmpty(sectionColumn);
            bool hasEmailColumn = !String.IsNullOrEmpty(emailColumn);

            if ((rosterContent != null) && (idColumn != null) && (nameColumn != null))
            {
                MemoryStream memStream = new MemoryStream();
                memStream.Write(rosterContent, 0, rosterContent.Length);
                memStream.Position = 0;

                List<RosterEntry> rosterEntries = parseRoster(memStream, idColumn, sectionColumn, nameColumn, name2Column, emailColumn);

                if (rosterEntries.Count > 0)
                {

                    // First check to make sure there are no duplicates in the ID table.
                    List<string> usedIdentifications = new List<string>();
                    foreach (RosterEntry entry in rosterEntries)
                    {
                        if (usedIdentifications.Contains(entry.Identification))
                        {
                            ViewBag.Error = "There are duplicate Student IDs in your roster. Please ensure the proper Student ID header was selected and check your roster file.";
                            return View("RosterError");
                        }
                        else
                        {
                            usedIdentifications.Add(entry.Identification);
                        }
                    }

                    // Make sure no student has the same ID as existing non-student members.
                    List<CourseUser> otherMembers = db.CourseUsers.Where(c => c.AbstractCourseID == ActiveCourseUser.AbstractCourseID && c.AbstractRoleID != (int)CourseRole.CourseRoles.Student).ToList();
                    foreach (CourseUser member in otherMembers)
                    {

                        if (usedIdentifications.Contains(member.UserProfile.Identification) && member.AbstractRoleID != (int)CourseRole.CourseRoles.Pending)
                        {
                            ViewBag.Error = "There is a " + "[" + member.AbstractRole.Name + "]" + " non-student (" + member.UserProfile.FirstName + " " + member.UserProfile.LastName + ") in the course with the same School ID as a student on the roster. Please check your roster and try again.";
                            return View("RosterError");
                        }
                    }

                    //Use the list of our old students to track changes between the current and new class roster.
                    //Students that exist on the old roster but do not appear on the new roster will
                    //be given the "withdrawn" status
                    var oldRoster = from c in db.CourseUsers
                                    where c.AbstractCourseID == ActiveCourseUser.AbstractCourseID
                                    &&
                                    c.AbstractRoleID == (int)CourseRole.CourseRoles.Student
                                    select c;

                    //Get all students in the course
                    List<CourseUser> otherStudents = db.CourseUsers.Where(c => c.AbstractCourseID == ActiveCourseUser.AbstractCourseID && c.AbstractRoleID == (int)CourseRole.CourseRoles.Student).ToList();

                    List<UserProfile> orphans = oldRoster.Select(cu => cu.UserProfile).ToList();
                    List<CourseUser> newRoster = new List<CourseUser>();
                    List<WhiteTable> newTable = new List<WhiteTable>();
                    string[] names = new string[2];
                    // Attach to users or add new user profile stubs.

                    //on new roster import clear the whitetable
                    clearWhiteTableOnRosterImport();

                    foreach (RosterEntry entry in rosterEntries)
                    {
                        UserProfile userWithAccount = getEntryUserProfile(entry);
                        if (userWithAccount != null)
                        {
                            CourseUser userIsPending = getPendingUserOnRoster(entry);
                            CourseUser existingCourseUser = getCourseUser(userWithAccount.ID);
                            
                            if (userIsPending != null)
                            {
                                userIsPending.AbstractRoleID = (int)CourseRole.CourseRoles.Student;
                                userIsPending.Hidden = false; //IMPORTANT; Pending users have hidden = true from RequestCourseJoin

                                //If the roster has section info in it, update the section
                                if (hasSectionColumn && (String.IsNullOrEmpty(userIsPending.Section.ToString()) || userIsPending.Section != entry.Section))
                                {
                                    userIsPending.Section = entry.Section;
                                }

                                //Else, leave the Section alone (default is Section 0)
                                

                                /*Leave emails alone for now (too easy for instructors to enter them wrong) but may be useful
                                  in the future?

                                //If the roster has email info in it, update the email and username (they are the same)
                                if (hasEmailColumn)
                                {
                                    string currentEmail = userIsPending.UserProfile.Email;
                                    string currentUserName = userIsPending.UserProfile.UserName;
                                    string rosterEmail = entry.Email;
                                    if (String.Compare(currentEmail, rosterEmail) != 0)
                                    {
                                        userIsPending.UserProfile.Email = rosterEmail;
                                    }
                                    if (String.Compare(currentUserName, rosterEmail != 0)
                                    {
                                        userIsPending.UserProfile.UserName = rosterEmail;
                                    }
                                    userIsPending.UserProfile.UserName = hasEmailColumn ? hasSectionColumn : 
                                }
                                */

                                orphans.Remove(userIsPending.UserProfile); 
                                db.Entry(userIsPending).State = EntityState.Modified;
                                continue;
                            }

                            //If the student is in a course already
                            else if (existingCourseUser != null)
                            {
                                //existingCourseUser.UserProfile = userWithAccount;
                                //existingCourseUser.UserProfile.Identification = entry.Identification;
                                
                                int oldSection = existingCourseUser.Section;

                                bool userInCourse = otherStudents.Contains(existingCourseUser) ? true : false;

                                //If the student is in the current course already, update their info
                                if (userInCourse)
                                {
                                    db.CourseUsers.Attach(existingCourseUser);
                                    if (hasSectionColumn && (String.IsNullOrEmpty(existingCourseUser.Section.ToString()) || existingCourseUser.Section != entry.Section))
                                    {
                                        //Update section in DB if necessary
                                        existingCourseUser.Section = entry.Section;
                                    }
                                    if (String.IsNullOrEmpty(existingCourseUser.MultiSection))
                                    {
                                        existingCourseUser.MultiSection = existingCourseUser.Section.ToString() + ",";
                                    }

                                    else
                                    {
                                        //Remove the old section from MultiSection
                                        if (existingCourseUser.MultiSection.Contains(oldSection.ToString()))
                                        {
                                            //Get the length of the old section PLUS the comma after it
                                            int lengthOfOldSection = oldSection.ToString().Length + 1;
                                            //Get the index of where the old section is
                                            var indexOfOldSection = existingCourseUser.MultiSection.IndexOf(oldSection.ToString());
                                            //Remove the old section and its following comma
                                            existingCourseUser.MultiSection = existingCourseUser.MultiSection.Remove(indexOfOldSection, lengthOfOldSection);
                                        }
                                        existingCourseUser.MultiSection += entry.Section.ToString() + ",";
                                    }
                                    db.SaveChanges();
                                    orphans.Remove(existingCourseUser.UserProfile);
                                }

                                //Otherwise, use their CourseUser info to create a course user for the current course
                                else
                                {
                                    CourseUser newCourseUser = new CourseUser(existingCourseUser);
                                    newCourseUser.AbstractCourseID = ActiveCourseUser.AbstractCourseID; //Add them to the course
                                    newCourseUser.UserProfile = userWithAccount;
                                    db.CourseUsers.Add(newCourseUser);

                                    //If the roster has section info in it, update the section
                                    if (hasSectionColumn && (String.IsNullOrEmpty(newCourseUser.Section.ToString()) || newCourseUser.Section != entry.Section))
                                    {
                                        newCourseUser.Section = entry.Section;
                                    }

                                    //Else, leave the Section alone (default is Section 0)

                                    if (String.IsNullOrEmpty(newCourseUser.MultiSection))
                                    {
                                        newCourseUser.MultiSection = newCourseUser.Section.ToString() + ",";
                                    }

                                    else
                                    {
                                        //Remove the old section from MultiSection
                                        if (newCourseUser.MultiSection.Contains(oldSection.ToString()))
                                        {
                                            //Get the length of the old section PLUS the comma after it
                                            int lengthOfOldSection = oldSection.ToString().Length + 1;
                                            //Get the index of where the old section is
                                            var indexOfOldSection = existingCourseUser.MultiSection.IndexOf(oldSection.ToString());
                                            //Remove the old section and its following comma
                                            existingCourseUser.MultiSection = existingCourseUser.MultiSection.Remove(indexOfOldSection, lengthOfOldSection);
                                        }
                                        newCourseUser.MultiSection += entry.Section.ToString() + ",";
                                    }
                                    db.SaveChanges();
                                    orphans.Remove(newCourseUser.UserProfile);
                                    emailCourseUser(newCourseUser);
                                }
                                rosterCount++;
                            }

                            //The student is not in this course or any other courses (first time users)
                            else
                            {
                                CourseUser existingUser = new CourseUser();

                                //yc: before using create course user, you must set the following
                                existingUser.UserProfile = userWithAccount;
                                existingUser.AbstractRoleID = (int)CourseRole.CourseRoles.Student;

                                //cs: Must set User Profile ID for future lookups
                                existingUser.AbstractCourseID = ActiveCourseUser.AbstractCourseID;
                                existingUser.UserProfileID = Convert.ToInt32(entry.Identification);
                                
                                //add section if a section column exists
                                if (sectionColumn != "")
                                {
                                    existingUser.Section = entry.Section;
                                    existingUser.MultiSection = existingUser.Section.ToString() + ",";
                                }

                                newRoster.Add(existingUser);

                                createCourseUser(existingUser);
                                rosterCount++;
                                
                                //email the user notifying them that they have been added to this course 
                                if (entry != null && !String.IsNullOrEmpty(entry.Email))
                                    emailCourseUser(existingUser);

                                orphans.Remove(existingUser.UserProfile);
                            }
                        }

                        //else the entry does not have a user profile, so WT them 
                        else
                        {
                            //create the WhiteTable that will hold the whitetableusers
                            WhiteTable whitetable = new WhiteTable();
                            whitetable.WhiteTableUser = new WhiteTableUser();


                            whitetable.Section = entry.Section;

                            whitetable.WhiteTableUser.Identification = entry.Identification;

                            if (entry.Name != null)
                            {
                                if (entry.Name.Contains(',')) //Assume "LastName, FirstName" format.
                                {
                                    names = entry.Name.Split(',');
                                    string[] parseFirstName = names[1].Trim().Split(' ');

                                    if (parseFirstName != null)
                                    {
                                        whitetable.WhiteTableUser.Name1 = parseFirstName[0];
                                        whitetable.WhiteTableUser.Name2 = names[0].Trim();
                                    }
                                    else
                                    {
                                        whitetable.WhiteTableUser.Name1 = names[1].Trim();
                                        whitetable.WhiteTableUser.Name2 = names[0].Trim();
                                    }
                                }
                                else //Assume "FirstName LastName" format. and No middle names.
                                {

                                    names = entry.Name.Trim().Split(' '); //Trimming trialing and leading spaces to avoid conflicts below
                                    if (names.Count() == 1) //Assume only last name
                                    {

                                        whitetable.WhiteTableUser.Name1 = string.Empty;
                                        whitetable.WhiteTableUser.Name2 = names[0];
                                    }
                                    else if (names.Count() == 2) //Only first and last name exist
                                    {

                                        whitetable.WhiteTableUser.Name1 = names[0];
                                        whitetable.WhiteTableUser.Name2 = names[1];
                                    }
                                    else //at least 1 Middle name exists. Use first and last entries in names
                                    {

                                        whitetable.WhiteTableUser.Name1 = names[0];
                                        whitetable.WhiteTableUser.Name2 = names[names.Count() - 1];
                                    }
                                }
                            }
                            else// the are nameless so the user will need to add this upon being added to a course 
                            {
                                whitetable.WhiteTableUser.Name1 = "Pending";
                                whitetable.WhiteTableUser.Name2 = string.Format("({0})", entry.Identification);
                            }
                            //check for emails

                            if (entry.Email != "")
                            {
                                whitetable.WhiteTableUser.Email = entry.Email;
                            }
                            else
                            {
                                whitetable.WhiteTableUser.Email = String.Empty;
                            }

                            createWhiteTableUser(whitetable);
                            wtCount++;
                            //will send email to white table user notifying them that they need to create an account to be added to the course 
                            //yc another check for emails
                            if (entry.Email != "")
                                emailWhiteTableUser(whitetable);
                        }

                    }// end foreach loop for whitetables



                    db.SaveChanges();

                    //withdraw all orphans
                    foreach (UserProfile orphan in orphans)
                    {
                        WithdrawUserFromCourse(orphan);
                    }
                }
            }
            else if ((idColumn == null))
            {
                ViewBag.Error = "You did not specify headers for Student ID. Please try again.";
                return View("RosterError");
            }
            else
            {
                ViewBag.Error = "Your roster file was not properly loaded. Please try again.";
                return View("RosterError");
            }

            Course thisCourse = (from d in db.Courses
                                 where d.ID == ActiveCourseUser.AbstractCourseID
                                 select d).FirstOrDefault();
            //if there is at least one assignmnet in the course that has teams/is team based
            if (thisCourse != null && thisCourse.Assignments.Count(a => a.HasTeams) > 0)
            {
                return RedirectToAction("Index", new { notice = "Roster imported with " + rosterCount.ToString() + " students and " + wtCount.ToString() + " whitelisted students. Please note that this course has an ongoing team-based assignment, and you will need to manually add the newly enrolled students to a team." });
            }

            return RedirectToAction("Index", new { notice = "Roster imported with " + rosterCount.ToString() + " students and " + wtCount.ToString() + " whitelisted students" });
        }

        //
        // GET: /Roster/Create
        [CanModifyCourse]
        [NotForCommunity]
        public ActionResult Create()
        {
            ViewBag.AbstractRoleID = new SelectList(db.CourseRoles, "ID", "Name");
            ViewBag.SchoolID = new SelectList(db.Schools, "ID", "Name");
            return View();
        }

        //
        // POST: /Roster/Create
        [CanModifyCourse]
        [NotForCommunity]
        [HttpPost]
        public ActionResult Create(CourseUser courseuser)
        {
            //Get all info
            var SchoolID = Request.Form["CurrentlySelectedSchool"];
            var AbstractRoleID = Request.Form["CurrentlySelectedAbstractRoleID"];
            var Section = Request.Form["Section"];
            var Identification = Request.Form["UserProfile.Identification"];
            //See if any are empty
            bool emptySchoolID = string.IsNullOrEmpty(SchoolID) ? true : false;
            bool emptyAbstractRoleID = string.IsNullOrEmpty(AbstractRoleID) ? true : false;
            bool emptySection = string.IsNullOrEmpty(Section) ? true : false;
            bool emptyIdentification = string.IsNullOrEmpty(Identification) ? true : false;

            bool isUpdate = false;

            if (emptySchoolID || emptyAbstractRoleID) //If one or both of these fields is missing, do not add the user
            {
                if (emptySchoolID && courseuser.UserProfile.SchoolID == 0) //If only the Abstract Role is missing, this message is not displayed
                {
                    ModelState.AddModelError("School", "The School field is required.");
                    ModelState.AddModelError("SchoolID", "");
                    //ViewBag.SchoolID = new SelectList(db.Schools, "ID", "Name");
                }
                else
                {
                    if (courseuser.UserProfile.SchoolID == 0) //Get the ID if it hasn't already been retrieved
                    {
                        courseuser.UserProfile.SchoolID = Convert.ToInt32(SchoolID);
                    }
                    //ViewBag.SchoolID = courseuser.UserProfile.SchoolID;
                }
                ViewBag.SchoolID = new SelectList(db.Schools, "ID", "Name");
                if (emptyAbstractRoleID && courseuser.AbstractRoleID == 0) //If only the School ID is missing, this message is not displayed
                {
                    ModelState.AddModelError("Course Role", "The Course Role field is required.");
                    ModelState.AddModelError("AbstractRoleID", "");
                    //ViewBag.AbstractRoleID = new SelectList(db.CourseRoles, "ID", "Name");
                }
                else
                {
                    if (courseuser.AbstractRoleID == 0)
                    {
                        courseuser.AbstractRoleID = Convert.ToInt32(AbstractRoleID);
                    }
                    //ViewBag.AbstractRoleID = courseuser.AbstractRoleID;
                    //if (ModelState.Keys.Contains("AbstractRoleID"))
                    //{
                    //    ModelState.Remove("AbstractRoleID");
                    //}
                    //if (ModelState.Keys.Contains("Course Role"))
                    //{
                    //    ModelState.Remove("Course Role");
                    //}
                }
                ViewBag.AbstractRoleID = new SelectList(db.CourseRoles, "ID", "Name");
                return View();
            }
            else
            {
                if (courseuser.AbstractRoleID == 0) //If courseUser AbstractRoleID hasn't already been set
                {
                    courseuser.AbstractRoleID = Convert.ToInt32(AbstractRoleID);
                }
                if (courseuser.UserProfile.SchoolID == 0)
                {
                    courseuser.UserProfile.SchoolID = Convert.ToInt32(SchoolID);
                }
                if (string.IsNullOrEmpty(courseuser.UserProfile.Identification))
                {
                    courseuser.UserProfile.Identification = Identification;
                }
                courseuser.Section = Convert.ToInt32(Section);
                courseuser.MultiSection = courseuser.Section.ToString() + ",";
                courseuser.AbstractCourseID = ActiveCourseUser.AbstractCourseID;
            }
            //if modelState isValid
            if (ModelState.IsValid && courseuser.AbstractRoleID != 0)
            {
                try
                {
                    //Get whether the student was in the course or not before add/update
                    isUpdate = DBHelper.IsUserInCourse(Identification, courseuser.AbstractCourseID);
                    createCourseUser(courseuser);
                }
                catch (Exception e)
                {
                    ModelState.AddModelError("", e.Message);
                    ViewBag.AbstractRoleID = new SelectList(db.CourseRoles, "ID", "Name");
                    ViewBag.SchoolID = new SelectList(db.Schools, "ID", "Name");
                    return View();
                }
            }

            Course thisCourse = ActiveCourseUser.AbstractCourse as Course;
            //if there is at least one assignment in the course that has teams/is team based
            if (thisCourse != null && thisCourse.Assignments.Count(a => a.HasTeams) > 0)
            {
                return RedirectToAction("Index", new { notice = "You have successfully added " + courseuser.UserProfile.LastAndFirst() + " to the course. Please note that this course has an ongoing team-based assignment, and " + courseuser.UserProfile.LastAndFirst() + " will need to be manually added to a team." });
            }
            else if (isUpdate)
            {
                return RedirectToAction("Index", new { notice = "You have successfully updated " + courseuser.UserProfile.LastAndFirst() + " in the course." });
            }
            return RedirectToAction("Index", new { notice = "You have successfully added " + courseuser.UserProfile.LastAndFirst() + " to the course." });

        }

        //
        // GET: /Roster/CreateByEmail
        [CanModifyCourse]
        public ActionResult CreateByEmail()
        {
            if (!(ActiveCourseUser.AbstractCourse is Community))
            {
                ViewBag.AbstractRoleID = new SelectList(db.CourseRoles, "ID", "Name");
            }
            else // Community Roles
            {
                ViewBag.AbstractRoleID = new SelectList(db.CommunityRoles, "ID", "Name");
            }
            return View();
        }

        //
        // POST: /Roster/CreateByEmail

        [HttpPost]
        [CanModifyCourse]
        public ActionResult CreateByEmail(CourseUser courseuser)
        {
            Course thisCourse = ActiveCourseUser.AbstractCourse as Course;
            //if modelState isValid
            if (ModelState.IsValid && courseuser.AbstractRoleID != 0)
            {
                try
                {
                    //yc this check for multiple emails added in. 
                    string temp = courseuser.UserProfile.UserName;
                    char[] delim = new char[] { ' ', ',', ';' };
                    string[] emails = temp.Split(delim, StringSplitOptions.RemoveEmptyEntries);
                    string[] invalidEmails = new string[emails.Count()];
                    int invalidCount = 0;

                    if (emails.Count() > 1)
                    {
                        // more than one 


                        foreach (String username in emails)
                        {
                            //find teh user profile
                            UserProfile user = (from u in db.UserProfiles
                                                where u.UserName == username
                                                select u).FirstOrDefault();
                            if (user != null)
                            {
                                CourseUser newUser = courseuser;
                                newUser.UserProfile = user;
                                newUser.UserProfileID = user.ID;

                                attachCourseUserByEmail(newUser);
                            }
                            else
                            {
                                //userprofile doenst exist.
                                invalidEmails[invalidCount] = username;
                                invalidCount++;
                            }
                            //create a copy of the course user
                        }
                        if (invalidCount > 0)//caught at least one invalid
                        {
                            //create a notice
                            string message = "The following email(s) could not be added because these users do not exist: ";
                            foreach (string invalid in invalidEmails)
                            {
                                if (invalid != "")
                                    message += invalid + ", ";
                            }
                            string noticeMessage = message.Substring(0, message.Length - 2);
                            noticeMessage += ".";

                            return RedirectToAction("Index", new { notice = noticeMessage });
                        }
                        else
                        {

                            //if there is at least one assignmnet in the course that has teams/is team based
                            if (thisCourse != null && thisCourse.Assignments.Count(a => a.HasTeams) > 0)
                            {
                                return RedirectToAction("Index", new { notice = emails.Count().ToString() + " users have been added to the course. Please note that this course has an ongoing team-based assignment, and you will need to manually add these users to a team." });
                            }
                            return RedirectToAction("Index", new { notice = emails.Count().ToString() + " users have been added to the course" });
                        }
                    }
                    else
                    {
                        //only one. do what you are originally intended for
                        attachCourseUserByEmail(courseuser);
                    }
                }
                catch (Exception e)
                {
                    ModelState.AddModelError("", e.Message);
                    ViewBag.AbstractRoleID = new SelectList(db.CourseRoles, "ID", "Name");
                    return View();
                }
            }

            //if there is at least one assignmnet in the course that has teams/is team based
            if (thisCourse != null && thisCourse.Assignments.Count(a => a.HasTeams) > 0)
            {
                return RedirectToAction("Index", new { notice = "You have successfully added " + courseuser.UserProfile.LastAndFirst() + " to the course. Please note that this course has an ongoing team-based assignment, and you will need to manually add these users to a team." });
            }
            return RedirectToAction("Index", new { notice = "You have successfully added " + courseuser.UserProfile.LastAndFirst() + " to the course." });
        }

        //yc: get the white table user, wrote getWhiteTableUser to narrow results in function, so do need to organize here
        //get
        [CanModifyCourse]
        public ActionResult EditWTUser(int wtuID)
        {
            WhiteTableUser wtUser = getWhiteTableUser(wtuID);
            //wtu has been loaded up
            //setup views

            return View(wtUser);
        }

        [HttpPost]
        [CanModifyCourse]
        public ActionResult EditWTUser(WhiteTableUser wtUser)
        {
            //wtu has been loaded up
            if (ModelState.IsValid)
            {
                if (wtUser.Email == null)
                    wtUser.Email = String.Empty;

                db.Entry(wtUser).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return RedirectToAction("Index");
        }


        /// <summary>
        /// yc: get- approve pending user for current course enrollment, clean up notifcation to instructor, change student status
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="courseId"> optional parameter, used in the case of adding a whitelisted user</param>
        /// <returns>back to index with notice the current pending user has been enrolled</returns>
        [CanModifyCourse]
        public ActionResult ApprovePending(int userId, int courseId = 0)
        {
            CourseUser pendingUser = getCourseUser(userId, courseId);
            Course thisCourse = null;
            if (ActiveCourseUser == null)
            {
                //Handle the case of whitelisted users
                //(a user wont be logged on here so ActiveCourseUser should be null)
                pendingUser = new CourseUser();
                pendingUser.ID = userId;
                thisCourse = db.AbstractCourses.FirstOrDefault(ac => ac.ID == courseId) as Course;
            }
            else
            {

                thisCourse = ActiveCourseUser.AbstractCourse as Course;
            }

            Notification n = null;
            if (ActiveCourseUser == null)
            {
                //do nothing for now...
                //in the current use case (Whitelisted user) we don't need to send a just approved whitelist user a notification.
            }
            else
            {
                n = db.Notifications.Where(item => item.SenderID == pendingUser.ID && item.RecipientID == ActiveCourseUser.ID).FirstOrDefault();
            }


            //there is not always a notification for a pending user, say a instructor manually adds them to the pending list?
            if (n != null)
            {
                n.Read = true;
                db.SaveChanges();
            }

            //set user to active student
            if (pendingUser.AbstractRoleID == (int)CourseRole.CourseRoles.Pending)
            {
                pendingUser.Hidden = false;
                pendingUser.AbstractRoleID = (int)CourseRole.CourseRoles.Student;
                db.Entry(pendingUser).State = EntityState.Modified;
                db.SaveChanges();
                addNewStudentToTeams(pendingUser);

                //if there is at least one assignmnet in the course that has teams/is team based
                if (thisCourse != null && thisCourse.Assignments.Count(a => a.HasTeams) > 0)
                {
                    if (pendingUser != null && pendingUser.UserProfile != null)
                    {
                        return RedirectToAction("Index", "Roster", new { notice = pendingUser.UserProfile.FirstName + " " + pendingUser.UserProfile.LastName + " has been enrolled into this course. Please note that this course has an ongoing team-based assignment, and you will need to manually add " + pendingUser.UserProfile.FirstName + " " + pendingUser.UserProfile.LastName + " to a team." });
                    }
                    else //case: pendingUser and/or UserProfile are null, so it must be just the role changing.
                    {
                        return RedirectToAction("Index", "Roster", new { notice = "1 user role has been changed. Please note that this course has an ongoing team-based assignment, and you will need to manually add the student to a team." });
                    }
                }

                if (pendingUser != null && pendingUser.UserProfile != null)
                {
                    return RedirectToAction("Index", "Roster", new { notice = pendingUser.UserProfile.FirstName + " " + pendingUser.UserProfile.LastName + " has been enrolled into this course." });
                }
                else //case: pendingUser and/or UserProfile are null, so it must be just the role changing.
                {
                    return RedirectToAction("Index", "Roster", new { notice = "1 user role has been changed for this course." });
                }
            }
            else if (pendingUser.AbstractRoleID == (int)CommunityRole.OSBLERoles.Pending)
            {
                pendingUser.Hidden = false;
                pendingUser.AbstractRoleID = (int)CommunityRole.OSBLERoles.Participant;
                db.Entry(pendingUser).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index", "Roster", new { notice = pendingUser.UserProfile.FirstName + " " + pendingUser.UserProfile.LastName + " is now a participant of this community." });
            }
            else
            {
                return RedirectToAction("Index", "Roster", new { notice = pendingUser.UserProfile.FirstName + " " + pendingUser.UserProfile.LastName + " IS NOT A PENDING USER." });
            }

        }
        /// <summary>
        /// yc: get- deny pending user for current course enrollmenet, clean up notifications to instructor
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        [CanModifyCourse]
        public ActionResult DenyPending(int userId)
        {
            CourseUser pendingUser = getCourseUser(userId);
            //db entry will no longer exists, save names for notice
            string firstName = pendingUser.UserProfile.FirstName;
            string lastName = pendingUser.UserProfile.LastName;

            //mark notification read
            Notification n = db.Notifications.FirstOrDefault(item => item.SenderID == pendingUser.ID && item.RecipientID == ActiveCourseUser.ID);
            /*if(n != null)
                n.Read = true;*/

            // remove the notification rather than mark it as read, there is a conflict with the 
            // database when we remove the pending user and they aren't associated with the notification anymore.
            if (null != n)
            {
                db.Notifications.Remove(n);
            }

            //remove the kid from the db
            db.CourseUsers.Remove(pendingUser);
            db.SaveChanges();

            if (pendingUser.AbstractRoleID == (int)CourseRole.CourseRoles.Pending)
                return RedirectToAction("Index", "Roster", new { notice = firstName + " " + lastName + " has been denied enrollment into this course." });
            else
                return RedirectToAction("Index", "Roster", new { notice = firstName + " " + lastName + " has been denied the ability to participate in this community." });
        }

        /// <summary>
        /// yc: creating a batch approval on pending users based on the current course
        /// </summary>
        /// <returns> to users page reflecting the changes</returns>
        [CanModifyCourse]
        public ActionResult BatchApprove()
        {
            Course thisCourse = ActiveCourseUser.AbstractCourse as Course;
            int count = 0;
            List<CourseUser> pendingUsers;
            //find all pending users for current course
            if (ActiveCourseUser.AbstractCourse.GetType() != typeof(Community)) //of type course
            {
                pendingUsers = (from c in db.CourseUsers
                                where c.AbstractCourseID == ActiveCourseUser.AbstractCourseID &&
                                c.AbstractRoleID == (int)CourseRole.CourseRoles.Pending
                                select c).ToList();

                count = pendingUsers.Count();

                foreach (CourseUser p in pendingUsers)
                {
                    p.Hidden = false;
                    p.AbstractRoleID = (int)CourseRole.CourseRoles.Student;
                    db.Entry(p).State = EntityState.Modified;
                    addNewStudentToTeams(p);
                }

                //get all notifications
                List<Notification> allUnreadNotifications = (from n in db.Notifications
                                                             where n.RecipientID == ActiveCourseUser.ID && !n.Read
                                                             select n).ToList();
                //get all notifications pertaining to the pendingUsers List
                List<Notification> pendingUsersNotifications = allUnreadNotifications.Where(item => pendingUsers.Contains(item.Sender)).ToList();
                //Mark them all as read
                foreach (Notification n in pendingUsersNotifications)
                {
                    n.Read = true;
                    db.Entry(n).State = EntityState.Modified;
                }


                db.SaveChanges();
                if (thisCourse != null && thisCourse.Assignments.Count(a => a.HasTeams) > 0)
                {
                    return RedirectToAction("Index", "Roster", new { notice = count.ToString() + " student(s) have been enrolled into this course. Please note that this course has an ongoing team-based assignment, and you will need to manually add the newly enrolled users to a team." });
                }
                return RedirectToAction("Index", "Roster", new { notice = count.ToString() + " student(s) have been enrolled into this course." });
            }
            else
            {
                pendingUsers = (from c in db.CourseUsers
                                where c.AbstractCourseID == ActiveCourseUser.AbstractCourseID &&
                                c.AbstractRoleID == (int)CommunityRole.OSBLERoles.Pending
                                select c).ToList();

                count = pendingUsers.Count();

                foreach (CourseUser p in pendingUsers)
                {
                    p.Hidden = false;
                    p.AbstractRoleID = (int)CommunityRole.OSBLERoles.Participant;
                    db.Entry(p).State = EntityState.Modified;
                }

                //get all notifications
                List<Notification> allUnreadNotifications = (from n in db.Notifications
                                                             where n.RecipientID == ActiveCourseUser.ID && !n.Read
                                                             select n).ToList();
                //get all notifications pertaining to the pendingUsers List
                List<Notification> pendingUsersNotifications = allUnreadNotifications.Where(item => pendingUsers.Contains(item.Sender)).ToList();
                //Mark them all as read
                foreach (Notification n in pendingUsersNotifications)
                {
                    n.Read = true;
                    db.Entry(n).State = EntityState.Modified;
                }

                db.SaveChanges();

                return RedirectToAction("Index", "Roster", new { notice = count.ToString() + " participant(s) have been added to the community." });
            }


        }

        /// <summary>
        /// yc: creating a batch denial on pending users based on the current course.
        /// </summary>
        /// <returns> to users page reflect the changes</returns>
        [CanModifyCourse]
        public ActionResult BatchDeny()
        {
            int count = 0;
            //find all pending users for current course
            List<CourseUser> pendingUsers;
            //find all pending users for current course
            if (ActiveCourseUser.AbstractCourse.GetType() != typeof(Community))
            {

                pendingUsers = (from c in db.CourseUsers
                                where c.AbstractCourseID == ActiveCourseUser.AbstractCourseID &&
                                c.AbstractRoleID == (int)CourseRole.CourseRoles.Pending
                                select c).ToList();
            }
            else
            {
                pendingUsers = (from c in db.CourseUsers
                                where c.AbstractCourseID == ActiveCourseUser.AbstractCourseID &&
                                c.AbstractRoleID == (int)CommunityRole.OSBLERoles.Pending
                                select c).ToList();
            }

            //get all notifications
            List<Notification> allUnreadNotifications = (from n in db.Notifications
                                                         where n.RecipientID == ActiveCourseUser.ID 
                                                         && n.SenderID == ActiveCourseUser.ID
                                                         && !n.Read
                                                         select n).ToList();
            //get all notifications pertaining to the pendingUsers List
            List<Notification> pendingUsersNotifications = allUnreadNotifications.Where(item => pendingUsers.Contains(item.Sender)).ToList();
            foreach (Notification n in pendingUsersNotifications)
            {
                //As seen in DenyPending, there is some conflict between removing the user before REMOVING (not marking as
                //read) notifictions that the user is associated with
                db.Notifications.Remove(n);
            }

            count = pendingUsers.Count();
            foreach (CourseUser p in pendingUsers)
            {
                db.CourseUsers.Remove(p);
            }
            db.SaveChanges();

            if (ActiveCourseUser.AbstractCourse.GetType() != typeof(Community))
                return RedirectToAction("Index", "Roster", new { notice = count.ToString() + " student(s) have been denied enrollment into this course." });
            else
                return RedirectToAction("Index", "Roster", new { notice = count.ToString() + " participants(s) have been denied the ability to join the community." });
        }


        /// <summary>
        /// yc: this function finds all students currently enrolled, and will turn them all into withdrawn students.
        /// </summary>
        /// <returns></returns>
        [CanModifyCourse]
        public ActionResult BatchWithdraw()
        {
            int count = 0;

            //find all students for current course
            List<CourseUser> students = (from c in db.CourseUsers
                                         where c.AbstractCourseID == ActiveCourseUser.AbstractCourseID &&
                                         c.AbstractRoleID == (int)CourseRole.CourseRoles.Student
                                         select c).ToList();
            count = students.Count();

            foreach (CourseUser p in students)
            {
                p.AbstractRoleID = (int)CourseRole.CourseRoles.Withdrawn;
                db.Entry(p).State = EntityState.Modified;
                db.SaveChanges();
            }

            return RedirectToAction("Index", "Roster", new { notice = count.ToString() + " student(s) have been withdrawn from this course" });
        }

        [CanModifyCourse]
        public ActionResult BatchClearWhiteTable()
        {
            int count = 0;

            //find all whitelisted students
            List<WhiteTableUser> students = (from c in db.WhiteTableUsers
                                             where c.CourseID == ActiveCourseUser.AbstractCourseID
                                             select c).ToList();
            count = students.Count();

            foreach (WhiteTableUser p in students)
            {
                db.WhiteTableUsers.Remove(p);
            }
            db.SaveChanges();
            return RedirectToAction("Index", "Roster", new { notice = count.ToString() + " whitelisted student(s) have been removed from this course" });
        }

        [CanModifyCourse]
        public ActionResult BatchDeleteWithdrawn()
        {
            int count = 0;

            //find all withdrawn students for current course
            List<CourseUser> students = (from c in db.CourseUsers
                                         where c.AbstractCourseID == ActiveCourseUser.AbstractCourseID &&
                                         c.AbstractRoleID == (int)CourseRole.CourseRoles.Withdrawn
                                         select c).ToList();
            count = students.Count();

            foreach (CourseUser p in students)
            {
                db.CourseUsers.Remove(p);
            }
            db.SaveChanges();
            return RedirectToAction("Index", "Roster", new { notice = count.ToString() + " withdrawn students have been removed from the course" });
        }

        [CanModifyCourse]
        public ActionResult ChangeWithdrawnToStudentRole(int userProfileID)
        {
            CourseUser CourseUser = getCourseUser(userProfileID);

            if (CanModifyOwnLink(CourseUser))
            {
                if (ModelState.IsValid)
                {
                    // make pending so we can approve pending to workaround issue of withdrawn user not being added to the assignments
                    CourseUser.AbstractRoleID = (int)CourseRole.CourseRoles.Pending;
                    db.Entry(CourseUser).State = EntityState.Modified;
                    db.SaveChanges();

                    using (RosterController rc = new RosterController())
                    {
                        rc.ApprovePending(CourseUser.UserProfileID, CourseUser.AbstractCourseID);
                    }
                    return RedirectToAction("Index");
                }
                ViewBag.UserProfileID = new SelectList(db.UserProfiles, "ID", "UserName", CourseUser.UserProfileID);
                ViewBag.AbstractCourse = new SelectList(db.Courses, "ID", "Prefix", CourseUser.AbstractCourseID);
                ViewBag.AbstractRoleID = new SelectList(db.CourseRoles, "ID", "Name", CourseUser.AbstractRoleID);

                return View(CourseUser);
            }
            return RedirectToAction("Index");
        }

        [CanModifyCourse]
        public ActionResult ChangeStudentToWithdrawnRole(int userProfileID)
        {
            CourseUser CourseUser = getCourseUser(userProfileID);

            if (CanModifyOwnLink(CourseUser))
            {
                if (ModelState.IsValid)
                {
                    CourseUser.AbstractRoleID = (int)CourseRole.CourseRoles.Withdrawn;
                    db.Entry(CourseUser).State = EntityState.Modified;
                    db.SaveChanges();
                    return RedirectToAction("Index");
                }
                ViewBag.UserProfileID = new SelectList(db.UserProfiles, "ID", "UserName", CourseUser.UserProfileID);
                ViewBag.AbstractCourse = new SelectList(db.Courses, "ID", "Prefix", CourseUser.AbstractCourseID);
                ViewBag.AbstractRoleID = new SelectList(db.CourseRoles, "ID", "Name", CourseUser.AbstractRoleID);
                return View(CourseUser);
            }
            return RedirectToAction("Index");
        }

        [CanModifyCourse]
        [HttpPost]
        public ActionResult ChangeStudentsToWithdrawnRole()
        {

            string temp = Request.Form["withdrawIDList"];
            string[] ids;
            ids = temp.Split(',', ' ');
            List<UserProfile> studentList = new List<UserProfile>();
            int parseID;

            List<int> idInts = new List<int>();

            //cast the id to int
            foreach (string id in ids)
            {
                if (Int32.TryParse(id, out parseID))
                {
                    idInts.Add(parseID);
                }
            }

            //grab all selected students in current class.
            if (null != ActiveCourseUser)
            {
                List<CourseUser> students = (from c in db.CourseUsers
                                             where c.AbstractCourseID == ActiveCourseUser.AbstractCourseID &&
                                             c.AbstractRoleID == (int)CourseRole.CourseRoles.Student &&
                                             idInts.Contains(c.UserProfileID)
                                             select c).ToList();

                foreach (CourseUser student in students)
                {
                    if (ModelState.IsValid)
                    {
                        student.AbstractRoleID = (int)CourseRole.CourseRoles.Withdrawn;
                        db.Entry(student).State = EntityState.Modified;
                        db.SaveChanges();
                    }
                }
            }

            return RedirectToAction("Index");
        }


        /// <summary>
        /// This will grab a list of user IDs from a post and change the users to students
        /// </summary>
        /// <returns>
        /// A view with updated roles
        /// </returns>

        [CanModifyCourse]
        [HttpPost]
        public ActionResult ChangeWithdrawnUsersToStudentRole()
        {
            string temp = Request.Form["enrollIDList"];
            string[] ids;
            ids = temp.Split(',', ' ');
            List<UserProfile> studentList = new List<UserProfile>();
            int parseID;

            List<int> idInts = new List<int>();

            //cast the id to int
            foreach (string id in ids)
            {
                if (Int32.TryParse(id, out parseID))
                {
                    idInts.Add(parseID);
                }
            }

            //grab all selected students in current class.
            if (null != ActiveCourseUser)
            {
                List<CourseUser> students = (from c in db.CourseUsers
                                             where c.AbstractCourseID == ActiveCourseUser.AbstractCourseID &&
                                             c.AbstractRoleID == (int)CourseRole.CourseRoles.Withdrawn &&
                                             idInts.Contains(c.UserProfileID)
                                             select c).ToList();

                foreach (CourseUser student in students)
                {
                    if (ModelState.IsValid)
                    {
                        student.AbstractRoleID = (int)CourseRole.CourseRoles.Student;
                        db.Entry(student).State = EntityState.Modified;
                        db.SaveChanges();
                    }
                }
            }

            return RedirectToAction("Index");
        }

        [CanModifyCourse]
        [HttpPost]
        public ActionResult removeSelectedUsers()
        {
            string temp = Request.Form["removeIDList"];
            string[] ids;
            ids = temp.Split(',', ' ');
            List<UserProfile> studentList = new List<UserProfile>();
            int parseID;

            List<int> idInts = new List<int>();

            //cast the id to int
            foreach (string id in ids)
            {
                if (Int32.TryParse(id, out parseID))
                {
                    idInts.Add(parseID);
                }
            }

            //find all withdrawn students for current course
            List<CourseUser> students = (from c in db.CourseUsers
                                         where c.AbstractCourseID == ActiveCourseUser.AbstractCourseID &&
                                         c.AbstractRoleID == (int)CourseRole.CourseRoles.Withdrawn &&
                                         idInts.Contains(c.UserProfileID)
                                         select c).ToList();
            int count = students.Count();

            foreach (CourseUser p in students)
            {
                db.CourseUsers.Remove(p);
            }
            var teamsWithNoMembers = (from at in db.AssignmentTeams
                                      where at.Team.TeamMembers.Count == 0
                                      select at).ToList();
            foreach (var team in teamsWithNoMembers)
            {
                db.AssignmentTeams.Remove(team);
            }
            db.SaveChanges();
            return RedirectToAction("Index", "Roster", new { notice = count.ToString() + " withdrawn students have been removed from the course" });
        }

        /// <summary>
        /// This is similar to the removeSelectedUsers method. This will remove all selected users for a community (except the current user)
        /// </summary>
        /// <returns></returns>
        [CanModifyCourse]
        [HttpPost]
        public ActionResult removeSelectedUsersCommunity()
        {
            string temp = Request.Form["removeIDList"];
            string[] ids;
            ids = temp.Split(',', ' ');
            List<UserProfile> studentList = new List<UserProfile>();
            int parseID;

            List<int> idInts = new List<int>();

            //cast the id to int
            foreach (string id in ids)
            {
                if (Int32.TryParse(id, out parseID))
                {
                    idInts.Add(parseID);
                }
            }

            //find all withdrawn students for current course
            List<CourseUser> students = (from c in db.CourseUsers
                                         where c.AbstractCourseID == ActiveCourseUser.AbstractCourseID &&
                                         idInts.Contains(c.UserProfileID)
                                         select c).ToList();

            int count = students.Count();

            foreach (CourseUser p in students)
            {
                if (p.UserProfileID != ActiveCourseUser.UserProfileID)
                {
                    db.CourseUsers.Remove(p);
                }
                else
                {
                    count--;
                }
            }

            db.SaveChanges();


            return RedirectToAction("Index", "Roster", new { notice = count.ToString() + " users removed from this community." });
        }

        //Students
        //

        //
        // GET: /Roster/Edit/5
        [CanModifyCourse]
        public ActionResult Edit(int userProfileID)
        {
            CourseUser CourseUser = getCourseUser(userProfileID);
            ViewBag.OldAbstractRoleID = CourseUser.AbstractRoleID;
            ViewBag.CurrentMultiSection = CourseUser.MultiSection;
            if (CanModifyOwnLink(CourseUser))
            {
                ViewBag.UserProfileID = new SelectList(db.UserProfiles, "ID", "UserName", CourseUser.UserProfileID);
                if (ActiveCourseUser.AbstractCourse is Course)
                {
                    ViewBag.AbstractRoleID = new SelectList(db.CourseRoles, "ID", "Name", CourseUser.AbstractRoleID);
                }
                else // Community Roles
                {
                    ViewBag.AbstractRoleID = new SelectList(db.CommunityRoles, "ID", "Name");
                }
                return View(CourseUser);
            }
            return RedirectToAction("Index");
        }

        //takes a list of ids
        //converts them to ints
        //changes the users with those ids to have the passed in section
        //if the section is -1 it means the user is in multiple sections
        //if the user is in section -2 it means the user is in all sections
        public bool EditSections(List<int> ids, int section, string sectionList, int courseID)
        {

            foreach (int id in ids)
            {

                CourseUser cu = (from c in db.CourseUsers
                                 where c.UserProfileID == id
                                 && c.AbstractCourseID == courseID
                                 select c).FirstOrDefault();

                if (CurrentUser.ID == cu.UserProfileID)
                    continue;

                if ((section == -1 || section == -2) && ((cu.AbstractRole.Name != "Instructor") && (cu.AbstractRole.Name != "TA") && (cu.AbstractRole.Name != "Moderator") && (cu.AbstractRole.Name != "Observer")))
                {
                    cu.Section = 0;
                    continue; //if the destination is multi sections and you're trying to move a student, block the move
                }

                if (cu.AbstractRole.Name == "Instructor") // if they're an instructor, they can only be in all sections
                {
                    cu.Section = -2;
                    cu.MultiSection = "all";
                }
                else if ((cu.AbstractRole.Name == "TA") && (ActiveCourseUser.Section != -2)) // if the current user is not in all sections, don't let them edit TAs
                {
                    continue;
                }


                    //if not an instructor
                else
                {
                    cu.Section = section;
                    if (section == -2) //if you're moving them to all sections
                        cu.MultiSection = "all";
                    else
                        cu.MultiSection = String.Copy(sectionList);
                }
            }

            db.SaveChanges();

            return true;
        }

        //
        // POST: /Roster/Edit/5

        [HttpPost]
        [CanModifyCourse]
        public ActionResult Edit(CourseUser courseUser)
        {
            if (CanModifyOwnLink(courseUser))
            {
                if (ModelState.IsValid)
                {
                    if (courseUser.AbstractRoleID == 1) //force the all sections constraint on professors
                        courseUser.Section = -2;

                    if (courseUser.AbstractRoleID == 3 && courseUser.Section < 0) //if students are being editted, don't allow them to be in section -1 or -2
                        courseUser.Section = 0;

                    //CurrentMultiSection
                    courseUser.MultiSection = Request.Form["CurrentMultiSection"];
                    db.Entry(courseUser).State = EntityState.Modified;
                    db.SaveChanges();

                    // Check to see if CourseUser's ROLE has changed from not student to student
                    int oldRoleId = -1, newRoleId = -1;
                    Int32.TryParse(Request.Form["OldAbstractRoleId"], out oldRoleId);
                    Int32.TryParse(Request.Form["AbstractRoleID"], out newRoleId);
                    if (oldRoleId != -1 && newRoleId != -1) //we successfully got both ID values
                    {
                        //The user role is changing from not student (e.g. TA/Instructor) to student role
                        if (oldRoleId != (int)CourseRole.CourseRoles.Student && newRoleId == (int)CourseRole.CourseRoles.Student)
                        {
                            // If so, make pending so we can approve pending to workaround issue of user not being added to the assignments                            
                            courseUser.AbstractRoleID = (int)CourseRole.CourseRoles.Pending;
                            db.Entry(courseUser).State = EntityState.Modified;
                            db.SaveChanges();
                            ApprovePending(courseUser.UserProfileID, courseUser.AbstractRoleID);
                            addNewStudentToTeams(courseUser);
                        }
                    }

                    return RedirectToAction("Index");
                }
                ViewBag.UserProfileID = new SelectList(db.UserProfiles, "ID", "UserName", courseUser.UserProfileID);
                ViewBag.AbstractCourse = new SelectList(db.Courses, "ID", "Prefix", courseUser.AbstractCourseID);
                ViewBag.AbstractRoleID = new SelectList(db.CourseRoles, "ID", "Name", courseUser.AbstractRoleID);
                return View(courseUser);
            }
            return RedirectToAction("Index");
        }


        //yc: not an inline remove
        //
        [CanModifyCourse]
        public ActionResult DeleteWTUser(int wtuID)
        {
            WhiteTableUser wtUser = getWhiteTableUser(wtuID);
            string name1 = wtUser.Name1;
            string name2 = wtUser.Name2;
            if (wtUser != null)
            {
                WhiteTable whiteTable = db.WhiteTable.Where(wt => wt.WhiteTableUserID == wtuID).FirstOrDefault();
                db.WhiteTableUsers.Remove(wtUser);
                db.WhiteTable.Remove(whiteTable);
                db.SaveChanges();
            }

            return RedirectToAction("Index", "Roster", new { notice = name1 + " " + name2 + " has been removed" });
        }
        // POST: /Roster/Delete/5

        [HttpPost]
        [CanModifyCourse]
        public ActionResult Delete(int userProfileID)
        {
            CourseUser CourseUser = getCourseUser(userProfileID);
            if ((CourseUser != null) && CanModifyOwnLink(CourseUser))
            {
                RemoveUserFromCourse(CourseUser.UserProfile);
            }
            else
            {
                Response.StatusCode = 403;
            }
            return View("_AjaxEmpty");
        }

        protected override void Dispose(bool disposing)
        {
            db.Dispose();
            base.Dispose(disposing);
        }

        private WhiteTableUser getWhiteTableUser(int wtuID)
        {
            return (from w in db.WhiteTableUsers
                    where w.ID == wtuID && w.CourseID == ActiveCourseUser.AbstractCourseID
                    select w).FirstOrDefault();
        }
        private CourseUser getCourseUser(int userProfileId, int courseId = 0)
        {
            if (ActiveCourseUser == null)
            {
                //Handle the case of whitelisted users
                //(a user wont be logged on here so ActiveCourseUser should be null)
                return (from c in db.CourseUsers
                        where c.AbstractCourseID == courseId
                        && c.UserProfileID == userProfileId
                        select c).FirstOrDefault();
            }
            else
            {
                return (from c in db.CourseUsers
                        where c.AbstractCourseID == ActiveCourseUser.AbstractCourseID
                        && c.UserProfileID == userProfileId
                        select c).FirstOrDefault();
            }


        }

        /// <summary>
        /// This says can the passed courseUser Modify the course and if so is there another teacher
        /// that can also modify this course if so it returns true else returns false
        /// Reason for check: Do not want instructors to delete themselves out of a course or remove their instructor status if there are no
        /// other instructors to take their place.
        /// </summary>
        /// <param name="courseUser"></param>
        /// <returns></returns>
        private bool CanModifyOwnLink(CourseUser courseUser)
        {
            var diffTeacher = (from c in db.CourseUsers
                               where (c.AbstractCourseID == courseUser.AbstractCourseID
                               && c.AbstractRole.CanModify == true
                               && c.UserProfileID != courseUser.UserProfileID)
                               select c);

            if (courseUser.UserProfileID != CurrentUser.ID || diffTeacher.Count() > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private List<string> getRosterHeaders(Stream roster)
        {

            StreamReader sr = new StreamReader(roster);
            CachedCsvReader csvReader = new CachedCsvReader(sr, true);

            List<string> getFieldHeaders = new List<string>();

            try
            {
                getFieldHeaders = csvReader.GetFieldHeaders().ToList();
            }
            catch (Exception)
            {
            }

            return getFieldHeaders;
        }


        private List<RosterEntry> parseRoster(Stream roster, string idNumberColumnName, string sectionColumnName, string nameColumnName, string name2ColumnName, string emailColumn)
        {
            StreamReader sr = new StreamReader(roster);
            CachedCsvReader csvReader = new CachedCsvReader(sr, true);

            List<RosterEntry> rosterData = new List<RosterEntry>();

            bool hasSectionInfo = false;

            if (sectionColumnName != null)
            {
                hasSectionInfo = csvReader.GetFieldHeaders().Contains(sectionColumnName);
            }

            csvReader.MoveToStart();
            while (csvReader.ReadNextRecord())
            {
                int sectionNum;
                RosterEntry entry = new RosterEntry();
                entry.Identification = csvReader[csvReader.GetFieldIndex(idNumberColumnName)];

                if (nameColumnName != "")
                {
                    if (name2ColumnName != "")
                    {
                        entry.Name = csvReader[csvReader.GetFieldIndex(name2ColumnName)] + ", " + csvReader[csvReader.GetFieldIndex(nameColumnName)];
                    }
                    else
                    {
                        entry.Name = csvReader[csvReader.GetFieldIndex(nameColumnName)];
                    }
                }
                else
                {
                    entry.Name = null;
                }
                if (hasSectionInfo)
                {
                    int.TryParse(csvReader[csvReader.GetFieldIndex(sectionColumnName)], out sectionNum);
                    entry.Section = sectionNum;
                }
                else
                {
                    entry.Section = 0;
                }
                if (emailColumn != "" && emailColumn != "None")
                {
                    entry.Email = csvReader[csvReader.GetFieldIndex(emailColumn)];
                }

                rosterData.Add(entry);
            }

            return rosterData;
        }

        [HttpGet, FileCache(Duration = 3600)]
        [Obsolete("Use UserController/Picture instead")]
        public ActionResult ProfilePicture(int userProfile)
        {
            return RedirectToAction("Picture", "User", new { id = userProfile });
        }

        /// <summary>
        /// This sets up everything for the courseUser and will create a new UserProfile if it doesn't not exist.
        /// </summary>
        /// <param name="courseuser">It must have section, role set, and a reference to UserProfile with Identification set</param>
        private void createCourseUser(CourseUser courseuser)
        {
            //This will return a user profile if they exist already or null if they don't
            var userProfile = (from c in db.UserProfiles
                               where c.Identification == courseuser.UserProfile.Identification
                               && c.SchoolID == courseuser.UserProfile.SchoolID
                               select c).FirstOrDefault();

            if (userProfile == null)
            {

                throw new Exception("No user exists with that Student ID!");

                //user doesn't exist so we got to make a new one
                //Create userProfile with the new ID
                //UserProfile up = new UserProfile();
                //up.CanCreateCourses = false;
                //up.IsAdmin = false;
                //up.SchoolID = CurrentUser.SchoolID;
                //up.Identification = courseuser.UserProfile.Identification;

                //if (courseuser.UserProfile.FirstName != null)
                //{
                //    up.FirstName = courseuser.UserProfile.FirstName;
                //    up.LastName = courseuser.UserProfile.LastName;
                //}
                //else
                //{
                //    up.FirstName = "Pending";
                //    up.LastName = string.Format("({0})", up.Identification);
                //}
                //db.UserProfiles.Add(up);
                //db.SaveChanges();

                ////Set the UserProfileID to point to our new student
                //courseuser.UserProfile = up;
                //courseuser.UserProfileID = up.ID;
                //courseuser.AbstractCourseID = ActiveCourseUser.AbstractCourseID;
            }
            else //The CourseUser has a UserProfile.
            {
                //Update input param courseuser UserProfile
                courseuser.UserProfile = userProfile;
                courseuser.UserProfileID = courseuser.UserProfile.ID;

                //This will return a course user if the student is in the course, and null if they are not
                var courseUserFromDB = (from c in db.CourseUsers
                                       where c.AbstractCourseID == courseuser.AbstractCourseID && c.UserProfileID == courseuser.UserProfileID
                                       select c).FirstOrDefault();
                
                //If the student isn't currently in the course, add them
                if (courseUserFromDB == null)
                {
                    db.CourseUsers.Add(courseuser);
                    db.SaveChanges();
                    addNewStudentToTeams(courseuser);
                }
                //Otherwise, update their Role and Section if necessary (always leave ID and School the same)
                else
                {
                    db.CourseUsers.Attach(courseUserFromDB);
                    if (courseUserFromDB.Section != courseuser.Section)
                    {
                        courseUserFromDB.Section = courseuser.Section;
                        courseUserFromDB.MultiSection = courseuser.MultiSection;
                    }
                    if (courseUserFromDB.AbstractRoleID != courseuser.AbstractRoleID)
                    {
                        courseUserFromDB.AbstractRoleID = courseuser.AbstractRoleID;
                    }
                    db.SaveChanges();
                }
            }
            ////Check uniqueness before adding the CourseUser and adding them to the Teams
            //if ((from c in db.CourseUsers
            //     where c.AbstractCourseID == courseuser.AbstractCourseID && c.UserProfileID == courseuser.UserProfileID
            //     select c).Count() == 0)
            //{
            //    db.CourseUsers.Add(courseuser);
            //    db.SaveChanges();
            //    addNewStudentToTeams(courseuser);
            //}
        }

        /// <summary>
        /// This method will add the new courseUser to all the various types of teams they need to be on for each assignment type.
        /// </summary>
        /// <param name="courseUser">A newly added courseUser, must of be role student</param>
        private void addNewStudentToTeams(CourseUser courseUser)
        {

            if (courseUser.AbstractRoleID == (int)CourseRole.CourseRoles.Student)
            {
                //If we already have assignments in the course, we need to add the new student into these assignments
                int currentCourseId = 0;

                if (ActiveCourseUser == null)
                {
                    //Handle the case of whitelisted users
                    //(a user wont be logged on here so ActiveCourseUser should be null)
                    currentCourseId = courseUser.AbstractCourseID;
                }
                else
                {
                    currentCourseId = ActiveCourseUser.AbstractCourseID;
                }

                List<Assignment> assignments = (from a in db.Assignments
                                                where a.CourseID == currentCourseId
                                                select a).ToList();


                foreach (Assignment a in assignments)
                {
                    bool present = false;
                    // First lets make sure the user isn't already in a team for this assignment
                    foreach (AssignmentTeam aTeam in a.AssignmentTeams)
                    {
                        foreach (TeamMember member in aTeam.Team.TeamMembers)
                        {
                            if (member.CourseUserID == courseUser.ID)
                            {
                                // If so, raise the present flag
                                present = true;
                                break;
                            }
                        }

                        if (present) break; // If present, exit loop and skip this assignment
                    }

                    if (present) continue;

                    TeamMember userMember = new TeamMember()
                    {
                        CourseUserID = courseUser.ID
                    };

                    Team team = new Team();

                    if (courseUser.UserProfile == null)
                    {
                        courseUser.UserProfile = db.UserProfiles.Where(up => up.ID == courseUser.UserProfileID).FirstOrDefault();
                    }

                    team.Name = courseUser.UserProfile.LastName + "," + courseUser.UserProfile.FirstName;
                    team.TeamMembers.Add(userMember);

                    db.Teams.Add(team);
                    db.SaveChanges();

                    AssignmentTeam assignmentTeam = new AssignmentTeam()
                    {
                        AssignmentID = a.ID,
                        Team = team,
                        TeamID = team.ID
                    };

                    db.AssignmentTeams.Add(assignmentTeam);
                    db.SaveChanges();

                    //If the assignment is a discussion assignment they must be on a discussion team.
                    if (a.Type == AssignmentTypes.DiscussionAssignment || a.Type == AssignmentTypes.CriticalReviewDiscussion)
                    {
                        DiscussionTeam dt = new DiscussionTeam();
                        dt.AssignmentID = a.ID;
                        dt.TeamID = assignmentTeam.TeamID;
                        a.DiscussionTeams.Add(dt);

                        //If the assignment is a CRD, the discussion team must also have an author team\
                        //Since this CRD will already be completely invalid for use (as its a CRD with only 1 member..) 
                        //we will do a small hack and have them be the author team and review team.
                        if (a.Type == AssignmentTypes.CriticalReviewDiscussion)
                        {
                            dt.AuthorTeamID = assignmentTeam.TeamID;
                        }
                        db.SaveChanges();
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to find an email address to match requested, and adds them to the course if it is found.
        /// </summary>
        /// <param name="courseUser"></param>
        private void attachCourseUserByEmail(CourseUser courseuser)
        {
            UserProfile up = db.UserProfiles.Where(u => u.UserName == courseuser.UserProfile.UserName).FirstOrDefault();

            if (up != null)
            {
                courseuser.UserProfile = up;
                courseuser.UserProfileID = up.ID;
            }
            else
            {
                throw new Exception("No user exists with that email address!");
            }

            courseuser.AbstractCourseID = ActiveCourseUser.AbstractCourseID;

            if ((from c in db.CourseUsers
                 where c.AbstractCourseID == courseuser.AbstractCourseID && c.UserProfileID == courseuser.UserProfileID
                 select c).Count() == 0)
            {
                db.CourseUsers.Add(courseuser);
                db.SaveChanges();

                //Adding the course user to teams so that they can access assignments
                addNewStudentToTeams(courseuser);
            }
            else
            {
                throw new Exception("This user is already in the course!");
            }
        }

        private void createWhiteTableUser(WhiteTable whitetable)
        {
            //do the same thing as createCourseUser but make the function work with our whitetable
            //This will return one if they exist already or null if they don't
            var user = (from c in db.WhiteTableUsers
                        where c.Identification == whitetable.WhiteTableUser.Identification
                        && c.SchoolID == ActiveCourseUser.UserProfile.SchoolID
                        select c).FirstOrDefault();
            if (user == null || user.CourseID != ActiveCourseUser.AbstractCourseID)
            {
                //user doesn't exist so we got to make a new one or the user exists, but not in this course, create a new user
                //Create userProfile with the new ID
                WhiteTableUser up = new WhiteTableUser();
                up.SchoolID = CurrentUser.SchoolID;
                up.Identification = whitetable.WhiteTableUser.Identification; //courseuser.UserProfile.Identification;                
                up.CourseID = ActiveCourseUser.AbstractCourseID; //is this used anywhere? previous implementation it was always 0...


                if (whitetable.WhiteTableUser.Name1 != null)
                {
                    up.Name1 = whitetable.WhiteTableUser.Name1;
                    if (whitetable.WhiteTableUser.Name2 != null)
                        up.Name2 = whitetable.WhiteTableUser.Name2;
                    else
                        up.Name2 = null;
                }
                else
                {
                    up.Name1 = "Pending";
                    up.Name2 = string.Format("({0})", up.Identification);
                }
                if (whitetable.WhiteTableUser.Email != "")
                    up.Email = whitetable.WhiteTableUser.Email;
                else
                {
                    //error check here
                    up.Email = "";
                }
                db.WhiteTableUsers.Add(up);
                db.SaveChanges();

                WhiteTable wt = new WhiteTable
                {
                    WhiteTableUserID = up.ID,
                    AbstractCourseID = ActiveCourseUser.AbstractCourseID,
                    Section = whitetable.Section,
                    Hidden = false
                };

                db.WhiteTable.Add(wt);
                db.SaveChanges();
            }


            else //If the CourseUser already has a UserProfile..
            {
                if (whitetable.WhiteTableUser.Name1 != null)
                {
                    user.Name1 = whitetable.WhiteTableUser.Name1;
                    user.Name2 = whitetable.WhiteTableUser.Name2;

                    db.Entry(user).State = EntityState.Modified;
                    db.SaveChanges();
                }
                whitetable.WhiteTableUser = user;
                whitetable.WhiteTableUserID = user.ID;

                db.Entry(whitetable).State = EntityState.Modified;
                db.SaveChanges();
            }
        }

        private void clearWhiteTableOnRosterImport()
        {
            var oldUsers = from d in db.WhiteTableUsers
                           where d.CourseID == ActiveCourseUser.AbstractCourseID
                           select d;

            foreach (var user in oldUsers)
            {
                db.WhiteTableUsers.Remove(user);

            }

            db.SaveChanges();
        }

        private UserProfile getEntryUserProfile(RosterEntry entry)
        {
            UserProfile possibleUser = (from d in db.UserProfiles
                                        where d.Identification == entry.Identification
                                        //&& d.UserName == entry.Email
                                        select d).FirstOrDefault();
            return possibleUser;
        }

        private CourseUser getPendingUserOnRoster(RosterEntry entry)
        {
            CourseUser pendingUser = (from d in db.CourseUsers
                                      where d.AbstractCourseID == ActiveCourseUser.AbstractCourseID
                                      && d.UserProfile.Identification == entry.Identification
                                      //&& d.UserProfile.UserName == entry.Email
                                      && d.AbstractRoleID == (int)CourseRole.CourseRoles.Pending
                                      select d).FirstOrDefault();

            return pendingUser;
        }

        private void emailCourseUser(CourseUser user)
        {
            if (user != null && user.AbstractCourse != null && user.UserProfile != null)
            {
                string subject = "Welcome to " + user.AbstractCourse.Name;
                string link = "https://plus.osble.org";

                string message = "Dear " + user.UserProfile.FirstName + " " + user.UserProfile.LastName + @", <br/>
            <br/>
            Congratulations! You have been enrolled in the following course at plus.osble.org: " + ActiveCourseUser.AbstractCourse.Name +
                "You may access this course by <a href='" + link + @"'>clicking on this link</a>. 
            <br/>
            <br/>
            ";

                message += @"Best regards,<br/>
            The OSBLE Team in the <a href='www.helplab.org'>HELP lab</a> at <a href='www.wsu.edu'>Washington State University</a>";

                Email.Send(subject, message, new List<MailAddress>() { new MailAddress(user.UserProfile.UserName) });
            }
        }

        private void emailWhiteTableUser(WhiteTable whitetable)
        {
            var WTU = whitetable.WhiteTableUser;

            string subject = "Welcome to OSBLE";
            string link = StringConstants.WebClientRoot + "Account/AcademiaRegister?email="
                + WTU.Email + "&firstname=" + WTU.Name1 + "&lastname=" + WTU.Name2 + "&identification=" + WTU.Identification;

            string message = "Dear " + WTU.Name1 + " " + WTU.Name2 + @", <br/>
                <br/>
                Congratulations! You have been enrolled in the following course at plus.osble.org: " + ActiveCourseUser.AbstractCourse.Name +
            " by " + ActiveCourseUser.UserProfile.FullName + ". In order to access this course, please create an OSBLE account with OSBLE first by " +
            "<a href='" + link + @"'>clicking on this link</a>. 
                <br/>
                <br/>
                ";

            message += @"Best regards,<br/>
                The OSBLE Team in the <a href='www.helplab.org'>HELP lab</a> at <a href='www.wsu.edu'>Washington State University</a>";

            if (WTU.Email != null)
                Email.Send(subject, message, new List<MailAddress>() { new MailAddress(WTU.Email) });

        }

        /// <summary>
        /// yc: this is for an individual email to be resent
        /// this would occur when an instructor clicks the email button on a student
        /// </summary>
        /// <param name="wtUser"></param>
        /// <returns></returns>
        [CanModifyCourse]
        public ActionResult resendWhiteTableEmail(int wtUserId)
        {

            //find user
            WhiteTableUser wtUser = (from c in db.WhiteTableUsers
                                     where c.ID == wtUserId &&
                                     c.CourseID == ActiveCourseUser.AbstractCourseID
                                     select c).FirstOrDefault();

            if (wtUser != null)
            {
                string subject = "Welcome to OSBLE";
                string link = StringConstants.WebClientRoot + "Account/AcademiaRegister?email="
                    + wtUser.Email + "&firstname=" + wtUser.Name1 + "&lastname=" + wtUser.Name2 + "&identification=" + wtUser.Identification;

                string message = "Dear " + wtUser.Name1 + " " + wtUser.Name2 + @", <br/>
                <br/>
                This email was sent to notify you that you have been added to " + ActiveCourseUser.AbstractCourse.Name +
                " by " + ActiveCourseUser.UserProfile.FullName + ". To access this course you need to create an account with OSBLE first. You may create an account " +
                "by <a href='" + link + @"'>following this link</a>. 
                <br/>
                <br/>
                ";
                message += @"Best regards,<br/>
                The OSBLE Team in the <a href='www.helplab.org'>HELP lab</a> at <a href='www.wsu.edu'>Washington State University</a>";

                if (null != wtUser.Email)
                    Email.Send(subject, message, new List<MailAddress>() { new MailAddress(wtUser.Email) });

                return RedirectToAction("Index", "Roster", new { notice = wtUser.Name2 + " " + wtUser.Name1 + " has been sent an email to join this course" });
            }
            else
            {
                return View("Index");
            }
        }

        /// <summary>
        /// This will take a list of Course User IDs and return the list of multiSection strings associated with those users in order
        /// </summary>
        /// <param name="studentID">This is a list of courseUser IDs</param>
        /// <returns>
        /// List of multiSection strings
        /// </returns>
        public string GetMultiSections(List<int> studentIDs)
        {
            List<string> multiSections = new List<string>();


            //this will go in order of the list passed in
            foreach (int id in studentIDs)
            {
                CourseUser temp = (from stu in db.CourseUsers
                                   where stu.AbstractCourseID == ActiveCourseUser.AbstractCourseID &&
                                   stu.UserProfileID == id
                                   select stu).FirstOrDefault();

                multiSections.Add(temp.MultiSection);
            }


            if (multiSections.Count == 0)
                return null;
            string output = new JavaScriptSerializer().Serialize(multiSections);
            return output;

        }

        /// <summary>
        /// yc: batch email sending for white listed users, no params, grabs its from active course users's course id
        /// </summary>
        /// <returns>back to index</returns>
        [CanModifyCourse]
        public ActionResult BatchEmailWhiteTable()
        {
            //getusers
            List<WhiteTableUser> wtu = (from w in db.WhiteTableUsers
                                        where w.CourseID == ActiveCourseUser.AbstractCourseID &&
                                        w.Email != ""
                                        select w).ToList();

            foreach (WhiteTableUser wtUser in wtu)
            {
                string subject = "Welcome to PLUS.OSBLE.org";
                string link = "https://plus.osble.org/Account/AcademiaRegister?email="
                    + wtUser.Email + "&firstname=" + wtUser.Name2 + "&lastname=" + wtUser.Name1 + "&identification=" + wtUser.Identification;

                string message = "Dear " + wtUser.Name2 + " " + wtUser.Name1 + @", <br/>
                <br/>
                This email was sent to notify you that you have been added to " + ActiveCourseUser.AbstractCourse.Name +
                " To access this course you need to create an account with OSBLE first. You may create an account " +
                "by <a href='" + link + @"'>following this link</a>. 
                <br/>
                <br/>
                ";
                message += @"Best regards,<br/>
                The OSBLE Team in the <a href='www.helplab.org'>HELP lab</a> at <a href='www.wsu.edu'>Washington State University</a>";
                Email.Send(subject, message, new List<MailAddress>() { new MailAddress(wtUser.Email) });
            }


            return RedirectToAction("Index", "Roster", new { notice = "Whitelisted users have been sent an invintation to join the course" });
        }


        /// <summary>
        /// This function enforces that all instructors are in section -2 and students are in section 0 or greater
        /// </summary>
        /// <returns>
        /// false if no users were updated
        /// true if users were updated and the page needs to be reloaded
        /// </returns>
        public bool SweepRoster()
        {
            bool flag = false;
            var users = (from c in db.CourseUsers
                         where c.AbstractCourseID == ActiveCourseUser.AbstractCourseID &&
                         c.AbstractRoleID == 1 || c.AbstractRoleID == 3
                         select c);

            foreach (CourseUser cu in users)
            {
                //if user is an instructor, make sure they're section -2
                if (cu.AbstractRoleID == 1 && cu.Section != -2)
                {
                    cu.Section = -2;
                    flag = true;
                }

                // if user is a student make sure they're in a non-negative section
                if (cu.AbstractRoleID == 3 && cu.Section < 0)
                {
                    cu.Section = 0;
                    flag = true;
                }
            }

            if (flag)
                db.SaveChanges();

            return flag;
        }
    }
}
