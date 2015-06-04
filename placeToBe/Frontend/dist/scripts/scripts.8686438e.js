"use strict";angular.module("frontendApp",["ngAnimate","ngCookies","ngResource","ngRoute","ngSanitize","ngTouch","ngMap"]).config(["$routeProvider",function(a){a.when("/",{templateUrl:"views/main.html",controller:"MainCtrl"}).when("/about",{templateUrl:"views/about.html",controller:"AboutCtrl"}).otherwise({redirectTo:"/"})}]),angular.module("frontendApp").controller("MainCtrl",["$scope","heatmapService",function(a,b){var c=new google.maps.Geocoder;a.city={name:"cologne"};var d=null;a.$on("mapInitialized",function(b,e){d=e,c.geocode({address:a.city.name},function(a,b){b==google.maps.GeocoderStatus.OK&&(e.setCenter(a[0].geometry.location),e.fitBounds(a[0].geometry.bounds))})}),a.drawEvents=function(){b.drawEvents("foo","bar",d,function(a){console.log(a)})}}]),angular.module("frontendApp").controller("AboutCtrl",["$scope",function(a){a.awesomeThings=["HTML5 Boilerplate","AngularJS","Karma"]}]),angular.module("frontendApp").directive("heatmapDirective",function(){return{templateUrl:"scripts/directives/heatmapdirective.html",restrict:"E",scope:{city:"="}}}),angular.module("frontendApp").service("heatmapService",["$http",function(a){var b="https://placetobe-koeln.azurewebsites.net/api/event";this.drawEvents=function(c,d,e,f){a.get(b+"/filter/cologne/now").success(function(a,b,c,d){f(a);var g=[];a.forEach(function(a){var b=new google.maps.LatLng(a.locationCoordinates.coordinates[0],a.locationCoordinates.coordinates[1]);g.push({location:b,weight:a.attending_count})}),g=new google.maps.MVCArray(g);var h=new google.maps.visualization.HeatmapLayer({data:g,map:e});h.setMap(e)})}}]);