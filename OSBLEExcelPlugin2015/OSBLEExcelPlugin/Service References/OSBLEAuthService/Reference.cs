﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace OSBLEExcelPlugin.OSBLEAuthService {
    using System.Runtime.Serialization;
    using System;
    
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Runtime.Serialization", "4.0.0.0")]
    [System.Runtime.Serialization.DataContractAttribute(Name="UserProfile", Namespace="http://schemas.datacontract.org/2004/07/OSBLE.Models.Users")]
    [System.SerializableAttribute()]
    public partial class UserProfile : object, System.Runtime.Serialization.IExtensibleDataObject, System.ComponentModel.INotifyPropertyChanged {
        
        [System.NonSerializedAttribute()]
        private System.Runtime.Serialization.ExtensionDataObject extensionDataField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string AuthenticationHashField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private bool CanCreateCoursesField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private int DefaultCourseField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private bool EmailAllActivityPostsField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private bool EmailAllNotificationsField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private bool EmailNewDiscussionPostsField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string FirstNameField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private int IDField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string IdentificationField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private bool IsAdminField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private bool IsApprovedField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string LastNameField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private OSBLEExcelPlugin.OSBLEAuthService.School SchoolField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private int SchoolIDField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string UserNameField;
        
        [global::System.ComponentModel.BrowsableAttribute(false)]
        public System.Runtime.Serialization.ExtensionDataObject ExtensionData {
            get {
                return this.extensionDataField;
            }
            set {
                this.extensionDataField = value;
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string AuthenticationHash {
            get {
                return this.AuthenticationHashField;
            }
            set {
                if ((object.ReferenceEquals(this.AuthenticationHashField, value) != true)) {
                    this.AuthenticationHashField = value;
                    this.RaisePropertyChanged("AuthenticationHash");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public bool CanCreateCourses {
            get {
                return this.CanCreateCoursesField;
            }
            set {
                if ((this.CanCreateCoursesField.Equals(value) != true)) {
                    this.CanCreateCoursesField = value;
                    this.RaisePropertyChanged("CanCreateCourses");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public int DefaultCourse {
            get {
                return this.DefaultCourseField;
            }
            set {
                if ((this.DefaultCourseField.Equals(value) != true)) {
                    this.DefaultCourseField = value;
                    this.RaisePropertyChanged("DefaultCourse");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public bool EmailAllActivityPosts {
            get {
                return this.EmailAllActivityPostsField;
            }
            set {
                if ((this.EmailAllActivityPostsField.Equals(value) != true)) {
                    this.EmailAllActivityPostsField = value;
                    this.RaisePropertyChanged("EmailAllActivityPosts");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public bool EmailAllNotifications {
            get {
                return this.EmailAllNotificationsField;
            }
            set {
                if ((this.EmailAllNotificationsField.Equals(value) != true)) {
                    this.EmailAllNotificationsField = value;
                    this.RaisePropertyChanged("EmailAllNotifications");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public bool EmailNewDiscussionPosts {
            get {
                return this.EmailNewDiscussionPostsField;
            }
            set {
                if ((this.EmailNewDiscussionPostsField.Equals(value) != true)) {
                    this.EmailNewDiscussionPostsField = value;
                    this.RaisePropertyChanged("EmailNewDiscussionPosts");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string FirstName {
            get {
                return this.FirstNameField;
            }
            set {
                if ((object.ReferenceEquals(this.FirstNameField, value) != true)) {
                    this.FirstNameField = value;
                    this.RaisePropertyChanged("FirstName");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public int ID {
            get {
                return this.IDField;
            }
            set {
                if ((this.IDField.Equals(value) != true)) {
                    this.IDField = value;
                    this.RaisePropertyChanged("ID");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string Identification {
            get {
                return this.IdentificationField;
            }
            set {
                if ((object.ReferenceEquals(this.IdentificationField, value) != true)) {
                    this.IdentificationField = value;
                    this.RaisePropertyChanged("Identification");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public bool IsAdmin {
            get {
                return this.IsAdminField;
            }
            set {
                if ((this.IsAdminField.Equals(value) != true)) {
                    this.IsAdminField = value;
                    this.RaisePropertyChanged("IsAdmin");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public bool IsApproved {
            get {
                return this.IsApprovedField;
            }
            set {
                if ((this.IsApprovedField.Equals(value) != true)) {
                    this.IsApprovedField = value;
                    this.RaisePropertyChanged("IsApproved");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string LastName {
            get {
                return this.LastNameField;
            }
            set {
                if ((object.ReferenceEquals(this.LastNameField, value) != true)) {
                    this.LastNameField = value;
                    this.RaisePropertyChanged("LastName");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public OSBLEExcelPlugin.OSBLEAuthService.School School {
            get {
                return this.SchoolField;
            }
            set {
                if ((object.ReferenceEquals(this.SchoolField, value) != true)) {
                    this.SchoolField = value;
                    this.RaisePropertyChanged("School");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public int SchoolID {
            get {
                return this.SchoolIDField;
            }
            set {
                if ((this.SchoolIDField.Equals(value) != true)) {
                    this.SchoolIDField = value;
                    this.RaisePropertyChanged("SchoolID");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string UserName {
            get {
                return this.UserNameField;
            }
            set {
                if ((object.ReferenceEquals(this.UserNameField, value) != true)) {
                    this.UserNameField = value;
                    this.RaisePropertyChanged("UserName");
                }
            }
        }
        
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        
        protected void RaisePropertyChanged(string propertyName) {
            System.ComponentModel.PropertyChangedEventHandler propertyChanged = this.PropertyChanged;
            if ((propertyChanged != null)) {
                propertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
            }
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Runtime.Serialization", "4.0.0.0")]
    [System.Runtime.Serialization.DataContractAttribute(Name="School", Namespace="http://schemas.datacontract.org/2004/07/OSBLE.Models")]
    [System.SerializableAttribute()]
    public partial class School : object, System.Runtime.Serialization.IExtensibleDataObject, System.ComponentModel.INotifyPropertyChanged {
        
        [System.NonSerializedAttribute()]
        private System.Runtime.Serialization.ExtensionDataObject extensionDataField;
        
        private int IDk__BackingFieldField;
        
        private string Namek__BackingFieldField;
        
        [global::System.ComponentModel.BrowsableAttribute(false)]
        public System.Runtime.Serialization.ExtensionDataObject ExtensionData {
            get {
                return this.extensionDataField;
            }
            set {
                this.extensionDataField = value;
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute(Name="<ID>k__BackingField", IsRequired=true)]
        public int IDk__BackingField {
            get {
                return this.IDk__BackingFieldField;
            }
            set {
                if ((this.IDk__BackingFieldField.Equals(value) != true)) {
                    this.IDk__BackingFieldField = value;
                    this.RaisePropertyChanged("IDk__BackingField");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute(Name="<Name>k__BackingField", IsRequired=true)]
        public string Namek__BackingField {
            get {
                return this.Namek__BackingFieldField;
            }
            set {
                if ((object.ReferenceEquals(this.Namek__BackingFieldField, value) != true)) {
                    this.Namek__BackingFieldField = value;
                    this.RaisePropertyChanged("Namek__BackingField");
                }
            }
        }
        
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        
        protected void RaisePropertyChanged(string propertyName) {
            System.ComponentModel.PropertyChangedEventHandler propertyChanged = this.PropertyChanged;
            if ((propertyChanged != null)) {
                propertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
            }
        }
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ServiceModel.ServiceContractAttribute(Namespace="", ConfigurationName="OSBLEAuthService.AuthenticationService")]
    public interface AuthenticationService {
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:AuthenticationService/GetActiveUser", ReplyAction="urn:AuthenticationService/GetActiveUserResponse")]
        OSBLEExcelPlugin.OSBLEAuthService.UserProfile GetActiveUser(string authToken);
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:AuthenticationService/GetActiveUser", ReplyAction="urn:AuthenticationService/GetActiveUserResponse")]
        System.Threading.Tasks.Task<OSBLEExcelPlugin.OSBLEAuthService.UserProfile> GetActiveUserAsync(string authToken);
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:AuthenticationService/ValidateUser", ReplyAction="urn:AuthenticationService/ValidateUserResponse")]
        string ValidateUser(string userName, string password);
        
        [System.ServiceModel.OperationContractAttribute(Action="urn:AuthenticationService/ValidateUser", ReplyAction="urn:AuthenticationService/ValidateUserResponse")]
        System.Threading.Tasks.Task<string> ValidateUserAsync(string userName, string password);
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public interface AuthenticationServiceChannel : OSBLEExcelPlugin.OSBLEAuthService.AuthenticationService, System.ServiceModel.IClientChannel {
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public partial class AuthenticationServiceClient : System.ServiceModel.ClientBase<OSBLEExcelPlugin.OSBLEAuthService.AuthenticationService>, OSBLEExcelPlugin.OSBLEAuthService.AuthenticationService {
        
        public AuthenticationServiceClient() {
        }
        
        public AuthenticationServiceClient(string endpointConfigurationName) : 
                base(endpointConfigurationName) {
        }
        
        public AuthenticationServiceClient(string endpointConfigurationName, string remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public AuthenticationServiceClient(string endpointConfigurationName, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public AuthenticationServiceClient(System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(binding, remoteAddress) {
        }
        
        public OSBLEExcelPlugin.OSBLEAuthService.UserProfile GetActiveUser(string authToken) {
            return base.Channel.GetActiveUser(authToken);
        }
        
        public System.Threading.Tasks.Task<OSBLEExcelPlugin.OSBLEAuthService.UserProfile> GetActiveUserAsync(string authToken) {
            return base.Channel.GetActiveUserAsync(authToken);
        }
        
        public string ValidateUser(string userName, string password) {
            return base.Channel.ValidateUser(userName, password);
        }
        
        public System.Threading.Tasks.Task<string> ValidateUserAsync(string userName, string password) {
            return base.Channel.ValidateUserAsync(userName, password);
        }
    }
}
