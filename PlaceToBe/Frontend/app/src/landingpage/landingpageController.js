'use strict';

/**
 * @ngdoc function
 * @name placeToBe.controller:landingpageController
 * @description
 * # MainCtrl
 * Controller of the frontendApp
 */
angular.module('placeToBe')
  .controller('landingpageController', function ($scope, loginService) {
        $scope.loginService = loginService;
  });
