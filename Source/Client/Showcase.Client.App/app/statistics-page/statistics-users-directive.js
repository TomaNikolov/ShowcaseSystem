﻿(function () {
    'use strict';

    var statisticsUsersDirective = function statisticsUsersDirective(jQuery) {
        return {
            restrict: 'A',
            templateUrl: '/app/statistics-page/statistics-users-directive.html',
            scope: {
                users: '='
            }
        };
    };

    angular
        .module('showcaseSystem.directives')
        .directive('statisticsUsers', ['jQuery', statisticsUsersDirective]);
}());