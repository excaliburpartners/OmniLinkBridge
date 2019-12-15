-- phpMyAdmin SQL Dump
-- version 3.5.1
-- http://www.phpmyadmin.net
--
-- Host: localhost
-- Generation Time: Dec 29, 2012 at 10:51 AM
-- Server version: 5.0.95
-- PHP Version: 5.2.10

SET SQL_MODE="NO_AUTO_VALUE_ON_ZERO";
SET time_zone = "+00:00";


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8 */;

--
-- Database: `hai`
--

-- --------------------------------------------------------

--
-- Table structure for table `log_areas`
--

CREATE TABLE IF NOT EXISTS `log_areas` (
  `log_area_id` int(10) unsigned NOT NULL auto_increment,
  `timestamp` datetime NOT NULL,
  `id` tinyint(4) NOT NULL,
  `name` varchar(12) NOT NULL,
  `fire` varchar(10) NOT NULL,
  `police` varchar(10) NOT NULL,
  `auxiliary` varchar(10) NOT NULL,
  `duress` varchar(10) NOT NULL,
  `security` varchar(20) NOT NULL,
  PRIMARY KEY  (`log_area_id`)
) ENGINE=MyISAM  DEFAULT CHARSET=latin1 AUTO_INCREMENT=1 ;

-- --------------------------------------------------------

--
-- Table structure for table `log_events`
--

CREATE TABLE IF NOT EXISTS `log_events` (
  `log_event_id` int(10) unsigned NOT NULL auto_increment,
  `timestamp` datetime NOT NULL,
  `name` varchar(12) NOT NULL,
  `status` varchar(10) NOT NULL,
  PRIMARY KEY  (`log_event_id`)
) ENGINE=MyISAM  DEFAULT CHARSET=latin1 AUTO_INCREMENT=1 ;

-- --------------------------------------------------------
--
-- Table structure for table `log_messages`
--

CREATE TABLE IF NOT EXISTS `log_messages` (
  `log_message_id` int(10) unsigned NOT NULL auto_increment,
  `timestamp` datetime NOT NULL,
  `id` smallint(6) NOT NULL,
  `name` varchar(12) NOT NULL,
  `status` varchar(10) NOT NULL,
  PRIMARY KEY  (`log_message_id`)
) ENGINE=MyISAM  DEFAULT CHARSET=latin1 AUTO_INCREMENT=1 ;

-- --------------------------------------------------------

--
-- Table structure for table `log_thermostats`
--

CREATE TABLE IF NOT EXISTS `log_thermostats` (
  `log_tstat_id` int(10) unsigned NOT NULL auto_increment,
  `timestamp` datetime NOT NULL,
  `id` tinyint(4) NOT NULL,
  `name` varchar(12) NOT NULL,
  `status` varchar(10) NOT NULL,
  `temp` smallint(6) NOT NULL,
  `heat` smallint(6) NOT NULL,
  `cool` smallint(6) NOT NULL,
  `humidity` smallint(6) NOT NULL,
  `humidify` smallint(6) NOT NULL,
  `dehumidify` smallint(6) NOT NULL,
  `mode` varchar(5) NOT NULL,
  `fan` varchar(5) NOT NULL,
  `hold` varchar(5) NOT NULL,
  PRIMARY KEY  (`log_tstat_id`)
) ENGINE=MyISAM  DEFAULT CHARSET=latin1 AUTO_INCREMENT=1 ;

-- --------------------------------------------------------

--
-- Table structure for table `log_units`
--

CREATE TABLE IF NOT EXISTS `log_units` (
  `log_unit_id` int(10) unsigned NOT NULL auto_increment,
  `timestamp` datetime NOT NULL,
  `id` smallint(6) NOT NULL,
  `name` varchar(12) NOT NULL,
  `status` varchar(15) NOT NULL,
  `statusvalue` smallint(6) NOT NULL,
  `statustime` smallint(6) NOT NULL,
  PRIMARY KEY  (`log_unit_id`)
) ENGINE=MyISAM  DEFAULT CHARSET=latin1 AUTO_INCREMENT=1 ;

-- --------------------------------------------------------

--
-- Table structure for table `log_zones`
--

CREATE TABLE IF NOT EXISTS `log_zones` (
  `log_zone_id` int(10) unsigned NOT NULL auto_increment,
  `timestamp` datetime NOT NULL,
  `id` smallint(6) NOT NULL,
  `name` varchar(16) NOT NULL,
  `status` varchar(10) NOT NULL,
  PRIMARY KEY  (`log_zone_id`)
) ENGINE=MyISAM  DEFAULT CHARSET=latin1 AUTO_INCREMENT=1 ;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
